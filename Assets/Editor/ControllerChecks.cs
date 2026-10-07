#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Controller support, measured in the real scene during Play Mode.
//
// Plugs in a virtual gamepad (nothing to hold, nothing saved into the scene)
// and drives it the way a player would: orbit, zoom and re-centre the overhead
// view, switch to first person and look and walk, then (stations as reach,
// playtest 3) open the dispenser's close-up with A from above, look round it,
// walk out of it, and stand behind the counter and across it. The player is put
// back where they started. Results go to the Console and to
// <project>/Logs/ControllerCheck/controller-check.txt.
public static class ControllerChecks
{
    private const string Menu = "Fixit Fidget/Checks/Controller (Play Mode)/Run controller check";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // A stack, so "yield return Seconds(.5f)" really waits: the steps nest
    // helper routines the way a Unity coroutine would.
    private static readonly Stack<IEnumerator> routine = new();
    private static Gamepad pad;
    private static readonly StringBuilder report = new();
    private static int failures, checks;
    private static CafeViewMode view;
    private static PlayerInteractor interactor;
    private static PlayerMovement movement;
    private static CharacterController capsule;
    private static Vector3 startPosition;
    private static Quaternion startRotation;
    private static bool startFirstPerson;

    [MenuItem(Menu)]
    private static void Run()
    {
        view = UnityEngine.Object.FindAnyObjectByType<CafeViewMode>();
        interactor = view != null ? view.GetComponent<PlayerInteractor>() : null;
        movement = view != null ? view.GetComponent<PlayerMovement>() : null;
        capsule = view != null ? view.GetComponent<CharacterController>() : null;
        if (view == null || interactor == null || movement == null || capsule == null)
        {
            Debug.LogError("[Controller check] The open scene has no player with CafeViewMode, PlayerInteractor, PlayerMovement and a CharacterController.");
            return;
        }
        report.Clear();
        failures = checks = 0;
        startPosition = view.transform.position;
        startRotation = view.transform.rotation;
        startFirstPerson = view.FirstPersonSelected;
        routine.Clear();
        routine.Push(Sequence());
        EditorApplication.update += Step;
    }

    [MenuItem(Menu, true)]
    private static bool CanRun() => EditorApplication.isPlaying && routine.Count == 0;

    private static void Step()
    {
        bool running = EditorApplication.isPlaying;
        try
        {
            while (running)
            {
                if (routine.Count == 0) { running = false; break; }
                IEnumerator top = routine.Peek();
                if (!top.MoveNext()) { routine.Pop(); continue; }
                if (top.Current is IEnumerator nested) { routine.Push(nested); continue; }
                break; // yielded null: wait for the next editor update
            }
        }
        catch (Exception exception)
        {
            Check(false, "Check stopped by an exception: " + exception.Message);
            Debug.LogException(exception);
            running = false;
        }
        if (running) return;
        EditorApplication.update -= Step;
        routine.Clear();
        Finish();
    }

    private static IEnumerator Sequence()
    {
        // The café's camera (like any game) ignores input while the Game view
        // is not focused, so a check run without focus would measure nothing.
        for (float until = Time.realtimeSinceStartup + 5f; !Application.isFocused && Time.realtimeSinceStartup < until;)
            yield return null;
        if (!Application.isFocused)
        {
            Check(false, "The Game view has keyboard focus (click inside it, then run the check again)");
            yield break;
        }
        pad = InputSystem.AddDevice<Gamepad>("Controller check pad");
        yield return Frames(4);

        // ---------- overhead view ----------
        if (view.FirstPersonSelected) view.SetFirstPerson(false);
        yield return Frames(4);
        Check(view.CanChangeView, "The café view is free to change (no station, dialogue or pause open)");

        float yaw0 = Get<float>(view, "isoYaw");
        Hold(new GamepadState { rightStick = new Vector2(1f, 0f) });
        yield return Seconds(.5f);
        Release();
        yield return Frames(3);
        float orbit = Mathf.DeltaAngle(yaw0, Get<float>(view, "isoYaw"));
        Check(PadInput.UsingPad, "Touching the pad switches the prompts to controller labels");
        Check(orbit > 20f, $"Right stick orbits the overhead view ({orbit:0} degrees in half a second)");
        Check(view.ControlsHint.Contains(PadInput.Label(PadButton.Select)),
            $"The view hint names the pad's button: \"{view.ControlsHint}\"");

        float pitch0 = Get<float>(view, "isoPitch");
        Hold(new GamepadState { rightStick = new Vector2(0f, -1f) });
        yield return Seconds(.4f);
        Release();
        yield return Frames(3);
        Check(Get<float>(view, "isoPitch") > pitch0 + 3f || Get<float>(view, "isoPitch") >= 67.9f,
            "Right stick down tilts the overhead view towards top-down");

        float distance0 = Get<float>(view, "isoDistance");
        Hold(new GamepadState { rightTrigger = 1f });
        yield return Seconds(.4f);
        Release();
        yield return Frames(3);
        float distance1 = Get<float>(view, "isoDistance");
        Check(distance1 < distance0 - 2f || distance1 <= view.minimumDistance + .01f,
            $"RT zooms in ({distance0:0.0} m to {distance1:0.0} m)");
        Hold(new GamepadState { leftTrigger = 1f });
        yield return Seconds(.4f);
        Release();
        yield return Frames(3);
        Check(Get<float>(view, "isoDistance") > distance1 + 2f, "LT zooms back out");

        yield return Press(GamepadButton.RightStick);
        Check(Mathf.Abs(Mathf.DeltaAngle(Get<float>(view, "isoYaw"), Get<float>(view, "homeIsoYaw"))) < .01f
              && Mathf.Abs(Get<float>(view, "isoDistance") - Get<float>(view, "homeIsoDistance")) < .01f,
            "R3 returns the overhead view to its authored angle");

        // ---------- walking in the overhead view ----------
        Vector3 before = view.transform.position;
        float fastest = 0f;
        Hold(new GamepadState { leftStick = new Vector2(0f, 1f) });
        for (float until = Time.realtimeSinceStartup + .45f; Time.realtimeSinceStartup < until;)
        {
            fastest = Mathf.Max(fastest, movement.CommandedVelocity.magnitude);
            yield return null;
        }
        Release();
        yield return Frames(3);
        Check(fastest > 2f, $"Left stick walks in the overhead view (up to {fastest:0.0} m/s)");
        Teleport(before);
        yield return Frames(3);

        // ---------- the movement assist (30 Sept): a thumb a little off the room's axis walks along it ----------
        // The stick direction that means "along the world's X axis" at the current camera yaw, then rolled 8°
        // off it (inside the core: pulled dead on) and 30° off it (outside the edge: the stick's own direction).
        if (movement.MovementAssist)
        {
            float yaw = view.MovementYaw;
            Vector4 bands = movement.AssistBands;   // room core, room edge, screen core, screen edge
            // A room axis (world +X): the stick a little off it walks dead along it.
            yield return StickHeading(StickFor(90f + bands.x * .8f, yaw), .35f);
            float offCore = Vector3.Angle(lastHeading, Vector3.right);
            Check(lastHeading.sqrMagnitude > 0f && offCore < .5f,
                $"Movement assist: the stick {bands.x * .8f:0}° off the room's axis walks dead along it ({offCore:0.0}° off; assist turned it by {lastAssist:0.0}°)");
            Teleport(before);
            yield return Frames(3);
            // A direction clear of every band (room axes and screen axes alike) is left to the stick.
            float free = FreeHeading(yaw, bands);
            yield return StickHeading(StickFor(free, yaw), .35f);
            float freeHeading = Mathf.Atan2(lastHeading.x, lastHeading.z) * Mathf.Rad2Deg;
            float drift = Mathf.Abs(Mathf.DeltaAngle(free, freeHeading));
            Check(lastHeading.sqrMagnitude > 0f && drift < 1.5f,
                $"Movement assist: a stick clear of the bands (world heading {free:0}°) is left alone ({drift:0.0}° from it, assist {lastAssist:0.0}°)");
            Teleport(before);
            yield return Frames(3);
            // Straight up on screen (the camera's yaw), pushed a little off it: the walk is exactly up the screen.
            Vector3 screenUp = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            yield return StickHeading(StickFor(yaw + bands.z * .8f, yaw), .35f);
            float offUp = Vector3.Angle(lastHeading, screenUp);
            Check(lastHeading.sqrMagnitude > 0f && offUp < .5f,
                $"Movement assist: the stick {bands.z * .8f:0}° off straight up walks straight up the screen ({offUp:0.0}° off; camera yaw {yaw:0}°)");
            Teleport(before);
            yield return Frames(3);
        }
        else Check(true, "Movement assist is off (the Inspector or the player's setting): its cases are skipped");

        // ---------- first person ----------
        yield return Press(GamepadButton.Select);
        Check(view.FirstPersonSelected, "View button switches to first person");
        // Look input is ignored on purpose while the camera blends between views.
        yield return AfterBlend();

        float look0 = Get<float>(view, "yaw");
        Hold(new GamepadState { rightStick = new Vector2(1f, 0f) });
        yield return Seconds(.5f);
        Release();
        yield return Frames(3);
        float turned = Mathf.DeltaAngle(look0, Get<float>(view, "yaw"));
        Check(turned > 30f, $"Right stick turns the first-person view ({turned:0} degrees in half a second)");

        float lookPitch = Get<float>(view, "pitch");
        Hold(new GamepadState { rightStick = new Vector2(0f, 1f) });
        yield return Seconds(.3f);
        Release();
        yield return Frames(3);
        Check(Get<float>(view, "pitch") < lookPitch - 5f, "Right stick up looks up");

        before = view.transform.position;
        fastest = 0f;
        Hold(new GamepadState { leftStick = new Vector2(0f, 1f) });
        for (float until = Time.realtimeSinceStartup + .45f; Time.realtimeSinceStartup < until;)
        {
            fastest = Mathf.Max(fastest, movement.CommandedVelocity.magnitude);
            yield return null;
        }
        Release();
        yield return Frames(3);
        Check(fastest > 2f, $"Left stick walks in first person (up to {fastest:0.0} m/s)");
        Teleport(before);

        yield return Press(GamepadButton.Select);
        Check(!view.FirstPersonSelected, "View button switches back to the overhead view");

        // ---------- stations as reach (playtest 3, 7 Oct 2026) ----------
        // Nothing is stepped up to any more: X does nothing by day. From above, A at the dispenser opens its close-up,
        // the right stick looks round it, and the left stick walks out of it (B steps back too); behind the counter Ace
        // counts as behind it, and across it he doesn't. (The bench and the counter's conversations need a device and a
        // customer: Fixit Fidget > Playtest > Stations as reach plays those.)
        StationInteractable drinks = interactor.DrinksStation, counter = interactor.CounterStation;
        if (drinks == null || drinks.StandPoint == null)
            Check(false, "The dispenser's station is in the scene, with a stand point");
        else
        {
            Teleport(drinks.StandPoint.position);
            yield return Frames(6);
            TMP_Text prompt = PromptText();
            string a = "[" + PadInput.Label(PadButton.South) + "]", x = "[" + PadInput.Label(PadButton.West) + "]";
            string text = prompt != null ? Flatten(prompt.text) : "";
            Check(text.Contains(a) && text.Contains(drinks.StationLabel), $"At the dispenser the prompt offers {a} {drinks.StationLabel}: \"{text}\"");
            Check(!text.Contains(x), $"and offers no {x}: stepping up is retired by day");
            yield return Press(GamepadButton.West);
            Check(!interactor.IsAtStation, "X steps up to nothing by day");
            yield return Press(GamepadButton.South);
            Check(interactor.CurrentStation == drinks, "A at the dispenser opens its close-up");
            yield return AfterBlend();

            BeverageLook look = drinks.GetComponentInChildren<BeverageLook>();
            if (look != null)
            {
                Quaternion rest = look.transform.rotation;
                Hold(new GamepadState { rightStick = new Vector2(1f, 0f) });
                yield return Seconds(.35f);
                Release();
                yield return Frames(3);
                float angle = Quaternion.Angle(rest, look.transform.rotation);
                Check(angle > 5f, $"Right stick looks around the dispenser ({angle:0} degrees)");
            }

            Hold(new GamepadState { leftStick = new Vector2(0f, -1f) });
            yield return Frames(8);
            Release();
            yield return Frames(4);
            Check(!interactor.IsAtStation, "The left stick walks out of the close-up");
            yield return AfterBlend();

            Teleport(drinks.StandPoint.position);
            yield return Frames(6);
            yield return Press(GamepadButton.South);
            Check(interactor.CurrentStation == drinks, "A opens it again");
            yield return AfterBlend();
            yield return Press(GamepadButton.East);
            Check(!interactor.IsAtStation, "B steps back from the close-up");
            yield return Frames(4);
        }

        if (counter == null || counter.StandPoint == null)
            Check(false, "The counter's station is in the scene, with a stand point");
        else
        {
            Teleport(counter.StandPoint.position);
            yield return Frames(6);
            Check(interactor.BehindCounter, "At the till Ace is behind the counter");
            CounterQueue queue = UnityEngine.Object.FindAnyObjectByType<CounterQueue>();
            if (queue != null && queue.SlotCount > 0)
            {
                Teleport(queue.SlotPoint(queue.SlotCount / 2).position);
                yield return Frames(6);
                Check(!interactor.BehindCounter, "Where the customers stand, he isn't");
            }
            TMP_Text prompt = PromptText();
            string x = "[" + PadInput.Label(PadButton.West) + "]";
            Check(prompt == null || !prompt.text.Contains(x), "The counter offers no X");
        }
    }

    private static void Finish()
    {
        try
        {
            if (interactor != null && interactor.IsAtStation) interactor.ExitStation();
            if (view != null)
            {
                Teleport(startPosition);
                view.transform.rotation = startRotation;
                if (view.FirstPersonSelected != startFirstPerson) view.SetFirstPerson(startFirstPerson);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); }
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        pad = null;

        string summary = $"[Controller check] {(failures == 0 ? "PASS" : "FAIL")}: {checks - failures}/{checks} checks passed.";
        string text = summary + "\n" + report;
        try
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "ControllerCheck");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "controller-check.txt"), text);
        }
        catch (Exception exception) { Debug.LogWarning("[Controller check] Could not write the report: " + exception.Message); }
        if (failures == 0) Debug.Log(text);
        else Debug.LogError(text);
    }

    // ---------- helpers ----------

    // The walk's direction over a held stick: the commanded velocity (after the assist) averaged over
    // the frames it was moving, and the assist's turn on the last of them.
    private static Vector3 lastHeading;
    private static float lastAssist;

    private static IEnumerator StickHeading(Vector2 stick, float seconds)
    {
        Vector3 sum = Vector3.zero;
        lastAssist = 0f;
        Hold(new GamepadState { leftStick = stick });
        yield return Frames(4);   // the stick settles through the Input System and the deadzone
        for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
        {
            Vector3 v = movement.CommandedVelocity;
            v.y = 0f;
            if (v.sqrMagnitude > .01f) { sum += v.normalized; lastAssist = movement.AssistApplied; }
            yield return null;
        }
        Release();
        yield return Frames(3);
        lastHeading = sum.sqrMagnitude > 0f ? sum.normalized : Vector3.zero;
    }

    // The stick (at four fifths deflection) that asks for a world heading, given the camera yaw the
    // movement turns the stick by: PlayerMovement does Euler(0, yaw, 0) * (x, 0, y).
    private static Vector2 StickFor(float worldHeading, float cameraYaw)
    {
        float rad = (worldHeading - cameraYaw) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * .8f;
    }

    // A world heading at least a band's width clear of every room axis and every screen axis.
    private static float FreeHeading(float cameraYaw, Vector4 bands)
    {
        for (float h = 0f; h < 360f; h += 2.5f)
        {
            float toRoom = Mathf.Abs(Mathf.DeltaAngle(PlayerMovement.NearestLine(h, 0f), h));
            float toScreen = Mathf.Abs(Mathf.DeltaAngle(PlayerMovement.NearestLine(h, cameraYaw), h));
            if (toRoom >= bands.y + 5f && toScreen >= bands.w + 5f) return h;
        }
        return 45f;
    }

    private static void Hold(GamepadState state) => InputSystem.QueueStateEvent(pad, state);
    private static void Release() => InputSystem.QueueStateEvent(pad, new GamepadState());

    private static IEnumerator Press(GamepadButton button)
    {
        Hold(new GamepadState().WithButton(button));
        yield return Frames(5);
        Release();
        yield return Frames(5);
    }

    private static IEnumerator AfterBlend()
    {
        yield return Frames(3);
        Camera main = Camera.main;
        CinemachineBrain brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
        for (float until = Time.realtimeSinceStartup + 4f; brain != null && brain.IsBlending && Time.realtimeSinceStartup < until;)
            yield return null;
        yield return Frames(3);
    }

    private static IEnumerator Frames(int count)
    {
        int target = Time.frameCount + count;
        while (Time.frameCount < target) yield return null;
    }

    private static IEnumerator Seconds(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

    private static void Teleport(Vector3 target)
    {
        target.y = view.transform.position.y;
        capsule.enabled = false;
        view.transform.position = target;
        capsule.enabled = true;
    }

    private static TMP_Text PromptText()
    {
        ShopUI hud = UnityEngine.Object.FindAnyObjectByType<ShopUI>();
        return hud != null ? Get<TMP_Text>(hud, "promptText") : null;
    }

    private static T Get<T>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Fields);
        return info != null ? (T)info.GetValue(target) : default;
    }

    private static string Flatten(string text) => text.Replace('\n', ' ').Replace('\r', ' ');

    private static void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
    }
}
#endif
