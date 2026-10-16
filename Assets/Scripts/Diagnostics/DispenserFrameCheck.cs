using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

// ---------------------------------------------------------------------------
// THE DISPENSER'S FRAME: A PLAY CHECK (playtest 4, 10 Oct 2026; the first-hour playtester: "Only 4 of the 6 paddles are
// fully in view: Americano is cut by the screen's edge and Latte is off it. Latte is Grace's drink.")
//
// Fixit Fidget > Playtest 4 > Bench > 2 - The dispenser's frame - play check (lab, Play Mode). A fresh Day 1 in the lab,
// nobody in the café. Ace at the dispenser the way a player gets there, the frame judged at 16:9 (whatever the Game view's
// own shape) against the dispenser's working face, the boxes of its colliders as the crosshair meets them: the six
// paddles, the six cup places under the nozzles, the cup stack and the discard basin.
//
//   The close-up from above (E beside the dispenser; 14 Oct, his note: "the overall look and feel is up and diagonal"): it
//   opens straight on, at Ace's eye (±5 cm), the dispenser's own heading (1°), no roll; the paddle row level on the screen
//   (0.5° at most; it was 13.9°); the smallest paddle 96 px or more at 1080p (it was 70); the whole face in frame with a margin
//   of 4% of the frame; turned onto the cup stack, the stack, Coffee and Tea stay in frame and the crosshair lands on it (the
//   far end leaving the frame is the trade-off, noted). Photos (Cinematics spec §4.2.5): 1 the close-up as it opens (1b turned
//   onto the cup stack), 2 the blend at about 25/50/75% from the home view and from a 45° orbit, 3 the overhead with Ace at the
//   stand point before E (the control); 5 and 6 first person. Fixit Fidget > Cinematics > Drinks close-up - straight on (scene) made the pose, with its before photo.
//   First person at the stand point: looking at the face's middle, all six paddles and the cup stack are in view. Then
//   aimed at the cup stack, as the Stations lab photographs it, the paddles still in view are counted and noted: in first
//   person the look, not the stand point, is what put Americano and Latte off the screen.
//
// Photos and report.txt go to Logs/Bench/dispenser-frame-<time>/. Nothing is saved in the scene; the lab's own save is the
// only file the game writes. The Game view must have focus, as for every lab check.
// ---------------------------------------------------------------------------
public sealed class DispenserFrameCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.Playtest4.DispenserFrame";
    /// <summary>The margin every target keeps inside the frame, as a share of the frame (Playtest4BenchSteps frames to it).</summary>
    public const float Margin = .04f;
    /// <summary>The frame the dispenser is judged in: 16:9, the game's own shape on a Steam player's screen.</summary>
    public const float Aspect = 16f / 9f;

    protected override string Title => "The dispenser's frame - play check";
    protected override string Tag => "[Dispenser frame]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Dispenser frame] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Dispenser frame check (this Play session only)");
        go.AddComponent<DispenserFrameCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Bench",
            $"dispenser-frame-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    PlayerMovement movement;
    PlayerInteractor interactor;
    CafeViewMode view;
    CustomerSpawner spawner;
    bool spawnerWas = true, firstPersonWas;

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && !DayClock.Instance.DayOver && DayClock.Instance.IsOpen && Camera.main != null,
            30f, "the lab's day is open");
        if (!lastWait) yield break;
        movement = FindAnyObjectByType<PlayerMovement>();
        interactor = movement != null ? movement.GetComponent<PlayerInteractor>() : null;
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        spawner = FindAnyObjectByType<CustomerSpawner>();
        Check(movement != null && interactor != null && view != null, "Ace is in the scene");
        if (movement == null || interactor == null || view == null) yield break;
        firstPersonWas = view.FirstPersonSelected;
        if (spawner != null) { spawnerWas = spawner.enabled; spawner.enabled = false; }
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)) Destroy(c.gameObject);

        StationInteractable drinks = interactor.DrinksStation;
        Check(drinks != null && drinks.StandPoint != null, "the dispenser's station is found, with its stand point");
        if (drinks == null || drinks.StandPoint == null) yield break;
        Face face = Face.Of(drinks);
        Check(face.Complete, $"its working face: {face.paddles.Count} paddles, {face.places.Count} cup places, " +
                             $"{(face.stack != null ? "the cup stack" : "NO cup stack")}, {(face.basin != null ? "the discard basin" : "NO discard basin")}");
        if (!face.Complete) yield break;
        Note("left to right: " + string.Join(", ", face.paddles.Select(p => p.name)));

        // ---------- the close-up from above (14 Oct: straight on, at Ace's eye; his note: "up and diagonal") ----------
        PlugInPad("Dispenser frame pad");
        yield return PadPress(GamepadButton.LeftShoulder);     // a touch of the pad, away from the dispenser: the game takes it as the one in hand
        yield return Frames(2);
        if (view.FirstPersonSelected) { view.SetFirstPerson(false); yield return AfterBlend(); }
        Transform stand = drinks.StandPoint;
        Vector3 home = view.OverheadHome;

        // The control (spec §4.2.5, photo 3): the overhead with Ace at the stand point, before E. It should look the same
        // before and after the close-up's change.
        view.OrbitTo(home.x, home.y, home.z);
        PutAce(stand.position, stand.eulerAngles.y);
        yield return Frames(6);
        yield return Photo("3-overhead-at-the-stand-point-before-E");

        // E from the home view, the blend photographed at about 25, 50 and 75% of its 0.75 s (timed from the press; the brain's
        // default blend), then the close-up as it opens.
        yield return EnterWatchingTheBlend(drinks, stand, home.x, "home");
        Check(interactor.IsAtStation && interactor.CurrentStation == drinks, "E beside the dispenser opens its close-up (the check's hand)");
        if (!interactor.IsAtStation) yield break;
        Camera cam = Camera.main;
        Vector3 eye = cam.transform.position;
        Quaternion turn = cam.transform.rotation;
        float fov = cam.fieldOfView;
        float least = Least(eye, turn, fov, face.All, out string worst);
        Check(least >= Margin - .002f, $"the whole dispenser is in the close-up's 16:9 frame as it opens: every paddle, cup place, the cup stack and the basin, " +
                                       $"{least:P1} in from the edge at the least ({worst}; {Margin:P0} is the bar)");
        // At Ace's eye, the dispenser's own heading, no roll.
        float eyeHeight = view.eyeHeight;
        float height = eye.y - stand.position.y;
        Vector3 ahead = Vector3.ProjectOnPlane(drinks.transform.forward, Vector3.up).normalized;
        float heading = Vector3.SignedAngle(ahead, Vector3.ProjectOnPlane(turn * Vector3.forward, Vector3.up), Vector3.up);
        float roll = Mathf.Abs(Vector3.SignedAngle(Vector3.ProjectOnPlane(turn * Vector3.right, Vector3.up), turn * Vector3.right, turn * Vector3.forward));
        Check(Mathf.Abs(height - eyeHeight) <= .05f && Mathf.Abs(heading) <= 1f && roll < .05f,
            $"the close-up opens at Ace's eye: {height:0.000} m above the floor ({eyeHeight:0.00} ±0.05), heading {heading:+0.00;-0.00}° from the dispenser's " +
            $"(1° at most), roll {roll:0.000}° (under 0.05)");
        float slant = RowSlant(eye, turn, fov, face);
        Check(Mathf.Abs(slant) <= .5f, $"the paddle row is level on the screen ({slant:+0.00;-0.00}°; 0.5° at most; it was 13.9° at the 10 Oct pose)");
        float smallest = SmallestPaddle(eye, turn, fov, face, out string which);
        Check(smallest >= 96f, $"and the paddles read: the smallest ({which}) is {smallest:0} px tall at 1080p (96 is the bar; it was 70)");
        Note($"the close-up's camera at {eye}, {fov:0}° tall; the Game view is {UnityEngine.Screen.width} x {UnityEngine.Screen.height} " +
             $"({cam.aspect:0.00}:1; judged at {Aspect:0.00}:1)");
        Note("from the edge, opening: " + Each(eye, turn, fov, face.All));
        yield return Photo("1-close-up-as-it-opens");
        Note($"photo 1: the row slants {slant:+0.0;-0.0}°, the smallest paddle {smallest:0} px");

        // Turned onto the cup stack, where a drink starts ("Aim at the cup stack to take a cup"): the stack and the paddles
        // beside it stay in the frame and the crosshair lands on it. The far end leaves the frame: the trade-off, as turning
        // your head does (spec §4.2.3), noted, not failed.
        BeverageLook look = drinks.GetComponentInChildren<BeverageLook>();
        Vector3 stackPoint = Point(face.stack);
        var near = NearStack(face);
        var keep = new List<(string name, Interactable thing)> { ("the cup stack", face.stack) };
        keep.AddRange(near);
        Quaternion onStack = Quaternion.LookRotation(stackPoint - eye, Vector3.up);
        float kept = Least(eye, onStack, fov, keep, out string worstKept);
        Check(kept >= 0f, $"turned onto the cup stack, the stack, {string.Join(" and ", near.Select(n => n.name))} are in the frame ({kept:P1} in at the least: {worstKept})");
        var gone = face.All.Where(t => Inside(eye, onStack, fov, Box(t.thing)) < 0f).Select(t => t.name).ToList();
        Note($"turned onto the cup stack, out of the frame (expected): {(gone.Count > 0 ? string.Join(", ", gone) : "nothing")}");
        if (AimAssist.Active && look != null)
        {
            look.StepAimTo(stackPoint);
            yield return Frames(2);
            yield return Await(() => !look.Aim.Gliding, 1.5f);
            yield return Frames(3);
            yield return Until(() => interactor.Focused == face.stack, 1.5f, "the look reaches it: the crosshair on the cup stack (within the look's own limits)");
            yield return Photo("1b-close-up-on-the-cup-stack");
        }
        else Note("the aim help is off (or no pad in hand), so the view wasn't turned for a photo: the numbers above are the turn's own");
        interactor.ExitStation();
        yield return AfterBlend();
        Check(!interactor.IsAtStation, "stepping back leaves the close-up");

        // The blend again from a 45° orbit (the moving part of "up and diagonal": it should no longer swing round).
        view.OrbitTo(home.x + 45f, home.y, home.z);
        PutAce(stand.position, stand.eulerAngles.y);
        yield return Frames(6);
        yield return EnterWatchingTheBlend(drinks, stand, home.x + 45f, "orbit-45");
        interactor.ExitStation();
        yield return AfterBlend();
        view.OrbitTo(home.x, home.y, home.z);
        yield return Frames(3);

        // ---------- first person at the stand point ----------
        Check(view.SetFirstPerson(true), "first person");
        yield return AfterBlend();
        PutAce(stand.position, stand.eulerAngles.y);
        yield return Frames(4);
        Vector3 middle = face.Middle;
        yield return LookAt(middle);
        yield return Frames(6);
        cam = Camera.main;
        var needed = face.paddles.Append((name: "the cup stack", thing: (Interactable)face.stack)).ToList();
        float fp = Least(cam.transform.position, cam.transform.rotation, cam.fieldOfView, needed, out string worstFp);
        Check(fp >= Margin - .002f, $"in first person at the stand point, looking at the dispenser's middle, all six paddles and the cup stack are in view, " +
                                    $"{fp:P1} in at the least ({worstFp}); {cam.fieldOfView:0}° tall, the eye {cam.transform.position.y - stand.position.y:0.00} m up, " +
                                    $"{Vector3.ProjectOnPlane(middle - cam.transform.position, Vector3.up).magnitude:0.00} m from the face's middle");
        Note("from the edge, looking at the middle: " + Each(cam.transform.position, cam.transform.rotation, cam.fieldOfView, face.All));
        yield return Photo("5-first-person-at-the-stand-point");
        yield return LookAt(Point(face.stack));
        yield return Frames(6);
        cam = Camera.main;
        var inView = face.paddles.Where(p => Inside(cam.transform.position, cam.transform.rotation, cam.fieldOfView, Box(p.thing)) >= 0f).Select(p => p.name).ToList();
        var cut = face.paddles.Where(p => !inView.Contains(p.name)).Select(p => p.name).ToList();
        Note($"aimed at the cup stack instead (as the Stations lab's photo is), {inView.Count} of 6 paddles are wholly in view" +
             (cut.Count > 0 ? $"; cut or off the screen: {string.Join(", ", cut)} (the look, not the stand point)" : ""));
        yield return Photo("6-first-person-on-the-cup-stack");
        yield return LookAt(middle);
        yield return Frames(3);
    }

    // ---------- the dispenser's working face ----------

    /// <summary>
    /// The dispenser's working face as the frame is judged: its paddles and its cup places (the places under the nozzles),
    /// left to right, the cup stack and the discard basin. Their colliders' boxes, as the crosshair meets them. The editor's
    /// step (Playtest4BenchSteps) frames the close-up on the same face with the same numbers.
    /// </summary>
    public sealed class Face
    {
        public readonly List<(string name, Interactable thing)> paddles = new List<(string name, Interactable thing)>();
        public readonly List<(string name, Interactable thing)> places = new List<(string name, Interactable thing)>();
        public BeverageCupSupply stack, basin;

        public bool Complete => paddles.Count == 6 && places.Count == 6 && stack != null && basin != null
                                && paddles.All(p => Box(p.thing).size != Vector3.zero) && Box(stack).size != Vector3.zero;

        /// <summary>Every target, the paddles first.</summary>
        public IEnumerable<(string name, Interactable thing)> All
        {
            get
            {
                foreach (var p in paddles) yield return (p.name + " paddle", p.thing);
                foreach (var p in places) yield return (p.name + " cup place", p.thing);
                if (stack != null) yield return ("the cup stack", stack);
                if (basin != null) yield return ("the discard basin", basin);
            }
        }

        /// <summary>The cup place farthest from the cup stack (the far end: Latte's).</summary>
        public Interactable FarPlace => places.Count == 0 || stack == null ? null
            : places.OrderByDescending(p => Vector3.Distance(Box(p.thing).center, Box(stack).center)).First().thing;
        public string FarName => places.Count == 0 || stack == null ? "" : places.First(p => p.thing == FarPlace).name;

        /// <summary>The middle of the whole face's box.</summary>
        public Vector3 Middle
        {
            get
            {
                bool any = false;
                Bounds all = default;
                foreach (var t in All)
                {
                    Bounds b = Box(t.thing);
                    if (!any) { all = b; any = true; } else all.Encapsulate(b);
                }
                return all.center;
            }
        }

        public static Face Of(StationInteractable drinks)
        {
            var f = new Face();
            if (drinks == null) return f;
            Transform d = drinks.transform;
            float Across(Interactable i) => Vector3.Dot(Box(i).center - d.position, d.right);
            foreach (BeverageControl c in drinks.GetComponentsInChildren<BeverageControl>().OrderBy(control => Across(control)))
                (c.dispenseButton ? f.paddles : f.places).Add((NameOf(c), c));
            foreach (BeverageCupSupply s in drinks.GetComponentsInChildren<BeverageCupSupply>())
            {
                if (s.discard) { if (f.basin == null) f.basin = s; }
                else if (f.stack == null) f.stack = s;
            }
            return f;
        }

        static string NameOf(BeverageControl c) =>
            c.slot != null && c.slot.drink != null && !string.IsNullOrEmpty(c.slot.drink.drinkName) ? Capital(c.slot.drink.drinkName)
            : c.transform.parent != null ? c.transform.parent.name : c.name;

        static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    /// <summary>A target's box: its collider's (the first in it), as the crosshair meets it.</summary>
    public static Bounds Box(Component thing)
    {
        if (thing == null) return new Bounds(Vector3.zero, Vector3.zero);
        Collider c = thing.GetComponentInChildren<Collider>();
        return c != null ? c.bounds : new Bounds(thing.transform.position, Vector3.zero);
    }

    static Vector3 Point(Component thing) => Box(thing).center;

    /// <summary>
    /// How far inside a 16:9 frame a box is, as a share of the frame: the least of its eight corners' four margins (negative:
    /// cut by the edge or behind). From a camera's place, its turn and its vertical field of view (degrees).
    /// </summary>
    public static float Inside(Vector3 eye, Quaternion turn, float fov, Bounds b)
    {
        float t = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
        Quaternion back = Quaternion.Inverse(turn);
        float least = float.PositiveInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 v = back * (p - eye);
            if (v.z <= .01f) return -1f;
            float x = .5f + .5f * v.x / (v.z * t * Aspect), y = .5f + .5f * v.y / (v.z * t);
            least = Mathf.Min(least, Mathf.Min(Mathf.Min(x, 1f - x), Mathf.Min(y, 1f - y)));
        }
        return least;
    }

    /// <summary>A box's height on the screen, as a share of the frame's height (0 when any of it is behind the camera).</summary>
    public static float Tall(Vector3 eye, Quaternion turn, float fov, Bounds b)
    {
        float t = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
        Quaternion back = Quaternion.Inverse(turn);
        float low = float.PositiveInfinity, high = float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 v = back * (p - eye);
            if (v.z <= .01f) return 0f;
            float y = .5f * v.y / (v.z * t);
            low = Mathf.Min(low, y);
            high = Mathf.Max(high, y);
        }
        return high - low;
    }

    /// <summary>The least margin among targets, and which target it is.</summary>
    public static float Least(Vector3 eye, Quaternion turn, float fov, IEnumerable<(string name, Interactable thing)> targets, out string worst)
    {
        float least = float.PositiveInfinity;
        worst = "";
        foreach (var t in targets)
        {
            float m = Inside(eye, turn, fov, Box(t.thing));
            if (m < least) { least = m; worst = t.name; }
        }
        return float.IsPositiveInfinity(least) ? -1f : least;
    }

    /// <summary>The smallest paddle's height on a 1080-pixel-tall screen, and which it is.</summary>
    public static float SmallestPaddle(Vector3 eye, Quaternion turn, float fov, Face face, out string which)
    {
        float smallest = float.PositiveInfinity;
        which = "";
        foreach (var p in face.paddles)
        {
            float px = Tall(eye, turn, fov, Box(p.thing)) * 1080f;
            if (px < smallest) { smallest = px; which = p.name; }
        }
        return float.IsPositiveInfinity(smallest) ? 0f : smallest;
    }

    /// <summary>
    /// The paddle row's slant on a 16:9 screen, degrees (positive: down to the right): the line from the leftmost paddle's middle
    /// to the rightmost's, in screen pixels. 13.9° at the 10 Oct pose ("up and diagonal"); level is 0.
    /// </summary>
    public static float RowSlant(Vector3 eye, Quaternion turn, float fov, Face face)
    {
        if (face == null || face.paddles.Count < 2) return 0f;
        Vector2 a = ScreenPoint(eye, turn, fov, Box(face.paddles[0].thing).center);
        Vector2 b = ScreenPoint(eye, turn, fov, Box(face.paddles[face.paddles.Count - 1].thing).center);
        Vector2 d = b - a;
        if (d.sqrMagnitude < 1e-6f) return 0f;
        if (d.x < 0f) d = -d;
        return -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }

    /// <summary>A point on a 1920 x 1080 (16:9) screen, pixels from the bottom left (a point behind the camera comes out far away).</summary>
    public static Vector2 ScreenPoint(Vector3 eye, Quaternion turn, float fov, Vector3 point)
    {
        float t = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
        Vector3 v = Quaternion.Inverse(turn) * (point - eye);
        if (v.z <= .01f) return new Vector2(1e5f, 1e5f);
        return new Vector2((.5f + .5f * v.x / (v.z * t * Aspect)) * 1920f, (.5f + .5f * v.y / (v.z * t)) * 1080f);
    }

    /// <summary>
    /// The paddles beside the cup stack that a player turned onto it must still see (spec §4.2.4: Coffee and Tea): by name, else
    /// the two nearest the stack.
    /// </summary>
    public static List<(string name, Interactable thing)> NearStack(Face face)
    {
        var near = new List<(string name, Interactable thing)>();
        if (face == null || face.stack == null) return near;
        foreach (string wanted in new[] { "Coffee", "Tea" })
            foreach (var p in face.paddles)
                if (string.Equals(p.name, wanted, StringComparison.OrdinalIgnoreCase)) { near.Add((p.name + " paddle", p.thing)); break; }
        if (near.Count == 2) return near;
        near.Clear();
        Vector3 stack = Box(face.stack).center;
        foreach (var p in face.paddles.OrderBy(p => Vector3.Distance(Box(p.thing).center, stack)).Take(2)) near.Add((p.name + " paddle", p.thing));
        return near;
    }

    static string Each(Vector3 eye, Quaternion turn, float fov, IEnumerable<(string name, Interactable thing)> targets) =>
        string.Join(", ", targets.Select(t => $"{t.name} {Inside(eye, turn, fov, Box(t.thing)):P0}"));

    // ---------- moving and looking ----------

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

    // First person: the view turned onto a point, as a mouse would (from the eye where the view puts it).
    IEnumerator LookAt(Vector3 point)
    {
        float floor = movement.transform.position.y - 1f;
        Vector3 eye = new Vector3(movement.transform.position.x, floor + view.eyeHeight - movement.EyeDrop, movement.transform.position.z);
        Vector2 a = AimAssist.Angles(eye, point);
        view.LookTo(a.x, a.y);
        yield return Frames(3);
    }

    // E from the overhead (the check's hand), and the brain's blend photographed at about 25, 50 and 75% of its 0.75 s, timed
    // from the press (at 15 fps a frame is 67 ms, so each lands within a frame of its mark; the report says where).
    IEnumerator EnterWatchingTheBlend(StationInteractable drinks, Transform stand, float yaw, string label)
    {
        float pressed = Time.realtimeSinceStartup;
        interactor.EnterStation(drinks);
        foreach (float share in new[] { .25f, .5f, .75f })
        {
            float when = pressed + share * .75f;
            while (Time.realtimeSinceStartup < when) yield return null;
            float elapsed = Time.realtimeSinceStartup - pressed;
            yield return Photo($"2-blend-from-{label}-{share * 100f:0}");
            Note($"the blend from the {label} view (yaw {yaw:0}°): photo at {elapsed:0.00} s of 0.75 ({elapsed / .75f:P0})");
        }
        yield return AfterBlend();
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
        if (interactor != null && interactor.IsAtStation) interactor.ExitStation();
        if (view != null && view.FirstPersonSelected != firstPersonWas) view.SetFirstPerson(firstPersonWas);
        if (spawner != null) spawner.enabled = spawnerWas;
    }
}
