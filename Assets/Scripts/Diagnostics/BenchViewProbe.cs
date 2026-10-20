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
//   * the forgiving bench (14 Oct; systems-forgiving-bench.md §6): at every quarter turn and on both faces, every place its
//     pieces may use (the cups the slot rule gives its screws, the wells, its new-part pad, the old-parts tray's slots) and
//     the five tools on the rest are in the frame, outside UI/UX's zones U1-U5, and not covered by the device (the camera's
//     line to each lands on it, not on the device); the device's outline on screen is reported against them;
//   * the feel targets T9 and T8 (restated 14 Oct 11:30 with the Producer's call 21): its layout is call 21's (every tool
//     rest at y 0; the phone's tray at (6, 133), the watch's at (26, 107)); at zoom 0 it fills at least 34 % (phone), 35 %
//     (camera) or 17 % (watch) of the frame's height, at the zoom limit at least 55 %; a screw head is at least 12 px across
//     at 1080p at zoom 0 (phone, watch);
//   * the second slot: the bench's spare slots lie off the mat now (in front of it, out of every close-up's frame: the TD's
//     §6.9), so a watch waiting there is out of the close-up's view and nothing has to make room for the phone;
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
            Check(StationInteractable.BenchHolds(firstJob), "second slot: a watch set down first, on the bench");
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
        if (inTheMiddle == null) yield return Framing(name, path == Watch ? "watch" : path == Camera_ ? "camera" : "phone", job);
        if (inTheMiddle != null)
        {
            // The spare slots lie off the mat (Bench 7, the TD's §6.9): the waiting watch is out of the frame, and stays put.
            bool outOfView = !AnyOfItOnScreen(inTheMiddle);
            Check(inspector.MadeRoomFor == null && (inTheMiddle.transform.position - middleBefore).magnitude < .005f && outOfView,
                $"{name}: the watch waiting on the bench lies off the mat, out of the close-up's frame ({(outOfView ? "none of it on screen" : "SOME OF IT ON SCREEN")}), and nothing had to make room");
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
        Check(BenchCards.Showing, $"{name}: the job card is up");
        Check(!BenchCards.CardOnScreen.Overlaps(device2D) && !BenchCards.ControlsOnScreen.Overlaps(device2D) && !BenchCards.HintOnScreen.Overlaps(device2D),
            $"{name}: the cards cover none of it (it {Px(device2D)}; the job card {Px(BenchCards.CardOnScreen)}; the controls {Px(BenchCards.ControlsOnScreen)}; the hint {Px(BenchCards.HintOnScreen)})");
        string list = Plain(BenchCards.ChecklistText), controls = Plain(BenchCards.HeldControlsText);
        Check(BenchCards.CurrentVerb.Length > 0 && BenchCards.Steps != null && BenchCards.Steps.Any(step => step.current),
            $"{name}: the job card gives the step to do now: \"{list}\"");
        Check(controls.IndexOf("turn it", System.StringComparison.OrdinalIgnoreCase) >= 0 && controls.Contains("Flip it over") && controls.Contains("Pick it up"),
            $"{name}: the full controls card says how to turn it, flip it and take it away: \"{controls}\"");

        // Every quarter turn, and turned over; at each, the mat's places and the tools in view and clear of the device.
        Places(name, "as it opens", job);
        foreach (float turn in new[] { 90f, 180f, -90f })
        {
            inspector.LayDown(turn);
            yield return Frames(3);
            Measure(name, $"turned to {turn:0}°", job);
            Places(name, $"turned to {turn:0}°", job);
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
        foreach (float turn in new[] { 0f, 90f, 180f, -90f })
        {
            inspector.LayDown(turn);
            yield return Frames(3);
            Places(name, $"turned over, at {turn:0}°", job);
        }
        inspector.LayDown(0f);
        yield return Frames(2);
        yield return Photo(name + "-3-turned-over");

        inspector.CancelInspection();
        yield return AfterBlend();
        if (inTheMiddle != null)
            Check((inTheMiddle.transform.position - middleBefore).magnitude < .01f,
                $"{name}: the waiting watch is where it lay when the close-up ended ({(inTheMiddle.transform.position - middleBefore).magnitude * 100f:0.0} cm from it)");
        Cleanup(device, job);
        yield return Frames(3);
    }

    // ---------- T9 and T8 (the feel targets, restated 14 Oct 11:30 with the Producer's call 21) ----------
    // The device's layout is call 21's (every tool rest at y 0; the phone's old-parts tray at (6, 133), the watch's at (26, 107),
    // the camera's at (-5, 115)), which Bench 7 writes and Bench 9 frames. Then, as he would see it: the device's share of the
    // frame's height at the layout view (zoom 0) and at the zoom limit (zoom 1), measured as Bench 9 measures it (the corners
    // of each renderer the bench counts, on screen), and a screw head's width on screen at 1080p at zoom 0 (T8).
    static readonly Dictionary<string, float> FillAtZoom0 = new Dictionary<string, float> { ["phone"] = .34f, ["camera"] = .35f, ["watch"] = .17f };
    const float FillAtZoom1 = .55f, HeadPxAtZoom0 = 12f;

    IEnumerator Framing(string name, string kind, JobBase job)
    {
        BenchLayout want = BenchLayout.ForKind(kind), has = DeviceDefinition.LayoutOf(job);
        bool same = has.SamePlaceAs(want);
        Check(same, $"{name}: its bench layout is the Producer's call 21 (tool rest at ({want.toolRestCentreX:0}, {want.toolRestCentreY:0}), old-parts tray at ({want.scrapTray.x:0}, {want.scrapTray.y:0})): it has {has}{(same ? "" : "; run Bench 7, which writes it")}");
        if (!has.HasFraming) Note($"{name}: no Bench 9 framing on its prefab yet, so T9 is measured on the scene's framing (Devices 2, then Bench 9, frame it)");

        float fill0 = FillOf(job);
        Check(fill0 >= FillAtZoom0[kind], $"{name}: T9 at zoom 0, {fill0 * 100f:0.0} % of the frame's height (≥ {FillAtZoom0[kind] * 100f:0} %)");
        if (kind != "camera")
        {
            float head = SmallestHeadPx(job, out int heads);
            Check(heads > 0 && head >= HeadPxAtZoom0, $"{name}: T8 at zoom 0, its smallest screw head is {head:0.0} px across at 1080p ({heads} screws; ≥ {HeadPxAtZoom0:0})");
        }

        // The zoom limit: the lens zoom set to 1 as the wheel or the D-pad would (the private field the zoom eases toward; Unity
        // needn't be in front for it), then back to the layout view.
        var zoomField = typeof(ItemInspector).GetField("zoom", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (zoomField == null || zoomField.FieldType != typeof(float)) { Note($"{name}: T9 at zoom 1 left out (ItemInspector has no float 'zoom' to set)"); yield break; }
        zoomField.SetValue(inspector, 1f);
        yield return Seconds(1f);   // the zoom eases at 12 a second: settled well within a second at any frame rate
        yield return Frames(2);
        float fill1 = FillOf(job);
        Check(inspector.Zoom > .99f && fill1 >= FillAtZoom1, $"{name}: T9 at zoom 1 (the limit), {fill1 * 100f:0.0} % of the frame's height (≥ {FillAtZoom1 * 100f:0} %)");
        yield return Photo(name + "-1b-zoom-limit");
        zoomField.SetValue(inspector, 0f);
        yield return Seconds(1f);
        yield return Frames(2);
        float back = FillOf(job);
        Check(inspector.Zoom < .01f && Mathf.Abs(back - fill0) < .01f, $"{name}: and back to the layout view ({back * 100f:0.0} %)");
    }

    // The device's share of the frame's height, as Bench 9 measures it: the corners of every renderer the bench counts
    // (DeviceBounds.Counts), each its own mesh's box turned with it, on screen.
    static float FillOf(JobBase job)
    {
        Camera cam = Camera.main;
        if (cam == null || job == null) return 0f;
        float y0 = float.MaxValue, y1 = float.MinValue;
        foreach (Renderer r in job.GetComponentsInChildren<Renderer>())
        {
            if (!DeviceBounds.Counts(r)) continue;
            MeshFilter filter = r.GetComponent<MeshFilter>();
            Mesh mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;
            Bounds b = mesh != null ? mesh.bounds : r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                if (mesh != null) corner = r.transform.TransformPoint(corner);
                Vector3 p = cam.WorldToScreenPoint(corner);
                if (p.z <= 0f) continue;
                y0 = Mathf.Min(y0, p.y);
                y1 = Mathf.Max(y1, p.y);
            }
        }
        return y1 > y0 ? (y1 - y0) / Mathf.Max(1, UnityEngine.Screen.height) : 0f;
    }

    // The smallest screw head's width on screen at 1080p (T8): its diameter across the view, at its head.
    static float SmallestHeadPx(JobBase job, out int count)
    {
        count = 0;
        Camera cam = Camera.main;
        if (cam == null || job == null) return 0f;
        float least = float.MaxValue;
        foreach (Screw screw in job.GetComponentsInChildren<Screw>())
        {
            Vector3 half = cam.transform.right * (screw.HeadDiameter * .5f);
            Vector3 a = cam.WorldToScreenPoint(screw.HeadPoint - half), b = cam.WorldToScreenPoint(screw.HeadPoint + half);
            if (a.z <= 0f || b.z <= 0f) continue;
            least = Mathf.Min(least, new Vector2(b.x - a.x, b.y - a.y).magnitude * 1080f / Mathf.Max(1, UnityEngine.Screen.height));
            count++;
        }
        return count > 0 ? least : 0f;
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
        // 19 Oct: the keys are icons (sprite tags), looked for in the rich text with the card's own helpers; the words after
        // them in the plain text. Never a bracketed button word.
        string rich = BenchCards.HeldControlsText ?? "", controls = Plain(rich);
        Check(LineHas(rich, "Move your hand", ControlHints.BenchHandStick) && LineHas(rich, "Turn it a quarter", ControlHints.BenchTurnStick)
              && LineHas(rich, "Flip it over", ControlHints.Flip) && LineHas(rich, "Pick it up", ControlHints.TakeFromBench) && !rich.Contains("["),
            $"pad: the controls card names the sticks, the flip key and the pick-up key (as icons, through ControlHints): \"{controls}\"");
        yield return Photo("pad-1-controls-card");

        // The stick swap (GameSettings.SwapBenchSticks, 12 Oct), in the card's words; the sticks themselves are Bench 3's.
        // The card must change with the setting (unless the player already had the sticks swapped), and name the hand's stick
        // through the same helper the card uses.
        string unswapped = rich;
        swapWas = GameSettings.SwapBenchSticks;
        swapTouched = true;
        GameSettings.SwapBenchSticks = true;
        yield return Seconds(.35f);   // the cards refresh every 0.15 s
        rich = BenchCards.HeldControlsText ?? "";
        controls = Plain(rich);
        Check((swapWas || rich != unswapped) && LineHas(rich, "Move your hand", ControlHints.BenchHandStick) && LineHas(rich, "Turn it a quarter", ControlHints.BenchTurnStick),
            $"pad: with the sticks swapped the card changes and says the hand's stick moves the hand and the other turns it: \"{controls}\"");
        yield return Photo("pad-1b-controls-card-sticks-swapped");
        GameSettings.SwapBenchSticks = swapWas;
        yield return Seconds(.35f);
        rich = BenchCards.HeldControlsText ?? "";
        controls = Plain(rich);
        Check(LineHas(rich, "Move your hand", ControlHints.BenchHandStick),
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

    // The mat's places this device's pieces may use and the tools on the rest (§6): in the frame, outside U1-U5, and not under
    // the device (the camera's line to each one's middle meets the device nowhere before it). The cups are the ones the slot
    // rule gives its screws (clear of all four of its resting quarter turns by 6 mm, in order); the pad its fresh part lies on
    // (or both while none is out); the wells when it has a cover; the old-parts tray's slots when it has a part to replace.
    void Places(string name, string when, JobBase job)
    {
        Camera cam = Camera.main;
        BenchStage stage = BenchStage.Instance;
        if (cam == null || stage == null) return;
        var places = new List<(string what, Vector3 at)>();
        int screws = job.GetComponentsInChildren<Screw>().Length;
        foreach (GameObject loose in job.LooseParts) if (loose != null) screws += loose.GetComponentsInChildren<Screw>().Length;
        if (screws > 0 && stage.FootprintOf(job, out BenchRules.MatBox device) && stage.cups != null)
        {
            int taken = 0;
            foreach (Transform cup in stage.cups)
            {
                if (cup == null || taken >= screws) continue;
                Vector2 mm = stage.WorldToMat(cup.position);
                if (!BenchRules.ClearOfDevice(new BenchRules.MatBox(mm.x, mm.y, 9f, 9f), device)) continue;
                places.Add((cup.name, cup.position));
                taken++;
            }
            Check(taken >= Mathf.Min(screws, 6), $"{name}, {when}: the slot rule finds a cup for each of its {screws} screws ({taken})");
        }
        bool cover = job.GetComponentsInChildren<RemovablePart>(true).Length > 0;
        if (cover)
        {
            if (stage.wellSmall != null) places.Add((stage.wellSmall.name, stage.wellSmall.position));
            if (stage.wellLarge != null) places.Add((stage.wellLarge.name, stage.wellLarge.position));
        }
        ReplaceablePart part = job.GetComponentInChildren<ReplaceablePart>();
        if (part != null)
        {
            Transform pad = part.Fresh != null ? stage.PlaceOf(part.Fresh) : null;
            if (pad != null) places.Add((pad.name, pad.position));
            else
            {
                if (stage.padSmall != null) places.Add((stage.padSmall.name, stage.padSmall.position));
                if (stage.padLarge != null) places.Add((stage.padLarge.name, stage.padLarge.position));
            }
            if (stage.scrapSlots != null) foreach (Transform slot in stage.scrapSlots) if (slot != null) places.Add((slot.name, slot.position));
        }
        foreach (ToolPickup tool in FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude))
            if (!tool.InHand && !tool.IsStandIn) places.Add((tool.displayName, tool.transform.position + tool.transform.rotation * tool.RestBox.center));

        var bad = new List<string>();
        int w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
        foreach (var (what, at) in places)
        {
            Vector3 s = cam.WorldToScreenPoint(at);
            if (s.z <= 0f || s.x < 0f || s.y < 0f || s.x > w || s.y > h) { bad.Add(what + " off the frame"); continue; }
            BenchStageContract.Zone? zone = BenchStageContract.ZoneAt(s, w, h);
            if (zone.HasValue && zone.Value != BenchStageContract.Zone.U6) { bad.Add($"{what} under {zone.Value}"); continue; }
            if (UnderTheDevice(cam, at, job)) bad.Add(what + " under the device");
        }
        Rect outline = OnScreen(job);
        Check(bad.Count == 0, $"{name}, {when}: its {places.Count} places and the tools in view, clear of the HUD's zones and of the device " +
                              $"(the device {Px(outline)}){(bad.Count > 0 ? ": " + string.Join(", ", bad) : "")}");
    }

    // The camera's line to a place meets the device before it does.
    static bool UnderTheDevice(Camera cam, Vector3 at, JobBase job)
    {
        Vector3 from = cam.transform.position, to = at - from;
        float length = to.magnitude;
        foreach (RaycastHit hit in Physics.RaycastAll(from, to / length, length - .002f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider != null && hit.collider.transform.IsChildOf(job.transform)) return true;
        return false;
    }

    // Anything of a device drawn on screen (its solid parts' corners).
    static bool AnyOfItOnScreen(JobBase job)
    {
        Camera cam = Camera.main;
        if (cam == null || !Solids(job, out Bounds b)) return false;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
            Vector3 v = cam.WorldToViewportPoint(corner);
            if (v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) return true;
        }
        return false;
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

    // 20 Oct: a card line carries its key as an icon wrapped in the card's key colour (BenchCards.K: <color=…><sprite…></color>),
    // so "the key, then the words" is checked line by line: the line that says the words must hold that key's icon.
    static bool LineHas(string rich, string words, string key)
    {
        foreach (string line in (rich ?? "").Split('\n'))
            if (line.Contains(words)) return line.Contains(key);
        return false;
    }

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
