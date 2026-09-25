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
// view, switch to first person and look and walk, then step up to the counter,
// the drink station and the repair bench and look around at each. The player
// is put back where they started. Results go to the Console and to
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

        // ---------- stations ----------
        foreach (StationInteractable station in UnityEngine.Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude))
        {
            if (station.StandPoint == null) continue;
            bool drinks = station.GetComponent<BeverageStation>() != null;
            string label = drinks ? "drink station" : station.IsWorkSurface ? "repair bench" : "counter";
            Teleport(station.StandPoint.position);
            yield return Frames(6);
            StationInteractable near = Get<StationInteractable>(interactor, "nearbyStation");
            Vector3 offset = view.transform.position - station.StandPoint.position;
            offset.y = 0f;
            report.AppendLine($"      at the {label}: {offset.magnitude:0.00} m from its stand point, nearest station "
                + (near != null ? near.StationLabel : "none"));
            TMP_Text prompt = PromptText();
            string stepUp = "[" + PadInput.Label(PadButton.West) + "]";
            if (prompt != null)
                Check(prompt.text.Contains(stepUp), $"Beside the {label} the prompt offers {stepUp}: \"{Flatten(prompt.text)}\"");

            yield return Press(GamepadButton.West);
            Check(interactor.CurrentStation == station, $"X steps up to the {label}");
            yield return AfterBlend();

            if (drinks)
            {
                BeverageLook look = null;
                foreach (BeverageLook candidate in UnityEngine.Object.FindObjectsByType<BeverageLook>(FindObjectsInactive.Exclude))
                    if (candidate.station == station) look = candidate;
                if (look != null)
                {
                    Quaternion rest = look.transform.rotation;
                    Hold(new GamepadState { rightStick = new Vector2(1f, 0f) });
                    yield return Seconds(.35f);
                    Release();
                    yield return Frames(3);
                    float angle = Quaternion.Angle(rest, look.transform.rotation);
                    Check(angle > 5f, $"Right stick looks around the {label} ({angle:0} degrees)");
                }
            }
            else
            {
                CinemachineCamera camera = Get<CinemachineCamera>(station, "stationCamera");
                CinemachinePanTilt panTilt = camera != null ? camera.GetComponent<CinemachinePanTilt>() : null;
                if (panTilt != null)
                {
                    float pan0 = panTilt.PanAxis.Value;
                    Hold(new GamepadState { rightStick = new Vector2(1f, 0f) });
                    yield return Seconds(.35f);
                    Release();
                    yield return Frames(3);
                    float panned = Mathf.Abs(Mathf.DeltaAngle(pan0, panTilt.PanAxis.Value));
                    Check(panned > 5f && panned < 90f,
                        $"Right stick looks around the {label} at a controllable speed ({panned:0} degrees in 0.35 s)");
                }
            }

            yield return Press(GamepadButton.East);
            Check(!interactor.IsAtStation, $"B steps back from the {label}");
            yield return Frames(4);
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
