using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Controller support for the café's verbs.
///
/// WHY DIRECT READS AND NOT MORE PLAYER INPUT ACTIONS
/// Most verbs in this project (F, Q, C, V, the conversation keys, the bench
/// clicks) already read the keyboard and mouse directly, because they depend on
/// who owns input this frame (a station, the inspector, a conversation, the
/// recap). Each of those places now also reads the matching pad button through
/// this class, so ownership rules stay exactly where they were and nothing new
/// has to be wired in the Inspector. Moving on the left stick still arrives
/// through PlayerInput's Move action.
///
/// Layout (Xbox names; PlayStation and Nintendo pads get their own labels):
///   Left stick   walk                       Right stick  look / orbit
///   A            interact (E)               B            back (Esc / right-click)
///   X            step up / step back (F)    Y            turn away (Q)
///   RB           switch hands (C)           LB / RB      left / right hand at the drink station
///   RT           use / click                LT           hold to speed up a circuit (Space)
///   LT / RT      zoom the overhead view     View         first person / overhead (V)
///   R3           re-centre the overhead view
///
/// UsingPad remembers which device the player touched last, so prompts can
/// say "A" instead of "E" and the mouse cursor can hide while a pad is in use.
/// </summary>
/// <summary>Controller buttons by position (South = A on Xbox, Cross on PlayStation).</summary>
public enum PadButton
{
    South, East, West, North,
    LeftShoulder, RightShoulder, LeftTrigger, RightTrigger,
    Select, Start, LeftStickPress, RightStickPress,
    DpadUp, DpadDown, DpadLeft, DpadRight,
}

public static class PadInput
{
    public enum Family { Xbox, PlayStation, Nintendo }

    // A little more than the Input System's own stick deadzone, so a resting
    // thumb never nudges the camera or flips the prompts to controller labels.
    private const float StickDeadzone = 0.2f;
    private const float TriggerDeadzone = 0.08f;
    private const float ActivityThreshold = 0.35f;

    private static readonly PadButton[] Buttons = (PadButton[])System.Enum.GetValues(typeof(PadButton));

    /// <summary>True when the last device the player touched was a gamepad.</summary>
    public static bool UsingPad { get; private set; }
    /// <summary>The frame UsingPad last changed. Useful for one-off refreshes.</summary>
    public static int SwitchedAtFrame { get; private set; } = -1;
    /// <summary>
    /// The pad the player last used (its labels are the ones shown). Reads
    /// below combine EVERY connected pad instead of trusting Gamepad.current:
    /// tools such as DS4Windows or Steam Input add a second, virtual pad, and
    /// Gamepad.current can flip to whichever of them sent the last report.
    /// </summary>
    public static Gamepad Pad => lastActive != null && lastActive.added ? lastActive : Gamepad.current;
    public static bool Connected => Gamepad.all.Count > 0;

    private static int trackedFrame = -1;
    private static Gamepad lastActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        UsingPad = false;
        SwitchedAtFrame = -1;
        trackedFrame = -1;
        lastActive = null;
    }

    // One small helper object per play session drives the tracking and the
    // on-screen pad cursor. Created at runtime only: nothing is saved into a
    // scene, and no scene needs new wiring for controller support to work.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Object.FindAnyObjectByType<PadInputDriver>() != null) return;
        var host = new GameObject("Controller support (runtime)");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<PadInputDriver>();
        host.AddComponent<PadCursor>();
    }

    /// <summary>Called once per frame, before gameplay reads input.</summary>
    internal static void Track()
    {
        if (trackedFrame == Time.frameCount) return;
        trackedFrame = Time.frameCount;

        bool padActive = false;
        var pads = Gamepad.all;
        for (int i = 0; i < pads.Count && !padActive; i++)
        {
            Gamepad candidate = pads[i];
            foreach (PadButton button in Buttons)
                if (Control(candidate, button).isPressed) { padActive = true; break; }
            if (!padActive)
                padActive = candidate.leftStick.ReadValue().sqrMagnitude > ActivityThreshold * ActivityThreshold
                    || candidate.rightStick.ReadValue().sqrMagnitude > ActivityThreshold * ActivityThreshold;
            if (padActive) lastActive = candidate;
        }

        bool desktopActive = false;
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) desktopActive = true;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
            || mouse.middleButton.wasPressedThisFrame || Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f
            || mouse.delta.ReadValue().sqrMagnitude > 16f))
            desktopActive = true;

        bool next = UsingPad;
        if (pads.Count == 0) next = false;
        else if (padActive) next = true;
        else if (desktopActive) next = false;
        if (next != UsingPad)
        {
            UsingPad = next;
            SwitchedAtFrame = Time.frameCount;
        }
    }

    // ---------- buttons ----------

    public static bool Pressed(PadButton button)
    {
        var pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
            if (Control(pads[i], button).wasPressedThisFrame) return true;
        return false;
    }

    public static bool Held(PadButton button)
    {
        var pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
            if (Control(pads[i], button).isPressed) return true;
        return false;
    }

    public static bool Released(PadButton button)
    {
        var pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
            if (Control(pads[i], button).wasReleasedThisFrame) return true;
        return false;
    }

    public static ButtonControl Control(Gamepad pad, PadButton button)
    {
        switch (button)
        {
            case PadButton.South: return pad.buttonSouth;
            case PadButton.East: return pad.buttonEast;
            case PadButton.West: return pad.buttonWest;
            case PadButton.North: return pad.buttonNorth;
            case PadButton.LeftShoulder: return pad.leftShoulder;
            case PadButton.RightShoulder: return pad.rightShoulder;
            case PadButton.LeftTrigger: return pad.leftTrigger;
            case PadButton.RightTrigger: return pad.rightTrigger;
            case PadButton.Select: return pad.selectButton;
            case PadButton.Start: return pad.startButton;
            case PadButton.LeftStickPress: return pad.leftStickButton;
            case PadButton.RightStickPress: return pad.rightStickButton;
            case PadButton.DpadUp: return pad.dpad.up;
            case PadButton.DpadDown: return pad.dpad.down;
            case PadButton.DpadLeft: return pad.dpad.left;
            default: return pad.dpad.right;
        }
    }

    // ---------- sticks and triggers ----------

    public static Vector2 LeftStick => Strongest(false);
    public static Vector2 RightStick => Strongest(true);
    public static float LeftTrigger => Deepest(false);
    public static float RightTrigger => Deepest(true);

    // Whichever connected pad is pushed furthest wins.
    private static Vector2 Strongest(bool right)
    {
        Vector2 best = Vector2.zero;
        var pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
        {
            Vector2 value = Stick(right ? pads[i].rightStick : pads[i].leftStick);
            if (value.sqrMagnitude > best.sqrMagnitude) best = value;
        }
        return best;
    }

    private static float Deepest(bool right)
    {
        float best = 0f;
        var pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
            best = Mathf.Max(best, Trigger(right ? pads[i].rightTrigger : pads[i].leftTrigger));
        return best;
    }

    /// <summary>
    /// Deadzoned stick with a gentle response curve: small deflections give
    /// fine aim, full deflection still reaches full speed.
    /// </summary>
    public static Vector2 Curved(Vector2 stick, float exponent = 1.6f)
    {
        float magnitude = Mathf.Clamp01(stick.magnitude);
        if (magnitude < 1e-4f) return Vector2.zero;
        return stick / stick.magnitude * Mathf.Pow(magnitude, exponent);
    }

    private static Vector2 Stick(StickControl stick)
    {
        if (stick == null) return Vector2.zero;
        Vector2 value = stick.ReadValue();
        float magnitude = value.magnitude;
        if (magnitude < StickDeadzone) return Vector2.zero;
        // Rescale so the usable range still starts at zero after the deadzone.
        float scaled = Mathf.Clamp01((magnitude - StickDeadzone) / (1f - StickDeadzone));
        return value / magnitude * scaled;
    }

    private static float Trigger(ButtonControl trigger)
    {
        if (trigger == null) return 0f;
        float value = trigger.ReadValue();
        return value < TriggerDeadzone ? 0f : Mathf.Clamp01((value - TriggerDeadzone) / (1f - TriggerDeadzone));
    }

    // ---------- labels ----------

    public static Family Kind
    {
        get
        {
            Gamepad pad = Pad;
            if (pad == null) return Family.Xbox;
            string name = (pad.layout + " " + pad.description.product + " " + pad.description.manufacturer).ToLowerInvariant();
            if (name.Contains("dualshock") || name.Contains("dualsense") || name.Contains("playstation")
                || name.Contains("sony")) return Family.PlayStation;
            if (name.Contains("switch") || name.Contains("pro controller") || name.Contains("nintendo"))
                return Family.Nintendo;
            return Family.Xbox;
        }
    }

    /// <summary>What this pad's own face prints for <paramref name="button"/>.</summary>
    public static string Label(PadButton button)
    {
        Family family = Kind;
        bool dualSense = family == Family.PlayStation && Pad != null
            && (Pad.layout + " " + Pad.description.product).ToLowerInvariant().Contains("dualsense");
        switch (button)
        {
            case PadButton.South: return family == Family.PlayStation ? "Cross" : family == Family.Nintendo ? "B" : "A";
            case PadButton.East: return family == Family.PlayStation ? "Circle" : family == Family.Nintendo ? "A" : "B";
            case PadButton.West: return family == Family.PlayStation ? "Square" : family == Family.Nintendo ? "Y" : "X";
            case PadButton.North: return family == Family.PlayStation ? "Triangle" : family == Family.Nintendo ? "X" : "Y";
            case PadButton.LeftShoulder: return family == Family.PlayStation ? "L1" : family == Family.Nintendo ? "L" : "LB";
            case PadButton.RightShoulder: return family == Family.PlayStation ? "R1" : family == Family.Nintendo ? "R" : "RB";
            case PadButton.LeftTrigger: return family == Family.PlayStation ? "L2" : family == Family.Nintendo ? "ZL" : "LT";
            case PadButton.RightTrigger: return family == Family.PlayStation ? "R2" : family == Family.Nintendo ? "ZR" : "RT";
            case PadButton.Select: return family == Family.PlayStation ? (dualSense ? "Create" : "Share") : family == Family.Nintendo ? "Minus" : "View";
            case PadButton.Start: return family == Family.PlayStation ? "Options" : family == Family.Nintendo ? "Plus" : "Menu";
            case PadButton.LeftStickPress: return family == Family.Xbox ? "LS" : "L3";
            case PadButton.RightStickPress: return family == Family.Xbox ? "RS" : "R3";
            case PadButton.DpadUp: return "D-pad up";
            case PadButton.DpadDown: return "D-pad down";
            case PadButton.DpadLeft: return "D-pad left";
            case PadButton.DpadRight: return "D-pad right";
            default: return button.ToString();
        }
    }
}
