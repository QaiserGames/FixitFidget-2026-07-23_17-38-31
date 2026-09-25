/// <summary>
/// Button names for on-screen prompts. Each one follows the device the player
/// used last: keyboard and mouse words by default, the pad's own labels
/// (A / Cross / B...) once a controller is being used. See PadInput for the
/// full controller layout.
/// </summary>
public static class ControlHints
{
    public static bool Pad => PadInput.UsingPad;

    /// <summary>Picks the keyboard-and-mouse or the controller wording.</summary>
    public static string Say(string keyboardAndMouse, string controller) => Pad ? controller : keyboardAndMouse;

    public static string Interact => Pad ? PadInput.Label(PadButton.South) : "E";
    public static string Station => Pad ? PadInput.Label(PadButton.West) : "F";
    public static string Refuse => Pad ? PadInput.Label(PadButton.North) : "Q";
    public static string Back => Pad ? PadInput.Label(PadButton.East) : "Esc";
    public static string SwitchHand => Pad ? PadInput.Label(PadButton.RightShoulder) : "C";
    public static string View => Pad ? PadInput.Label(PadButton.Select) : "V";
    public static string Boost => Pad ? PadInput.Label(PadButton.LeftTrigger) : "Space";
    public static string LeftHand => Pad ? PadInput.Label(PadButton.LeftShoulder) : "Left click";
    public static string RightHand => Pad ? PadInput.Label(PadButton.RightShoulder) : "Right click";
    /// <summary>The "use / click" verb: "Click" on a mouse, "RT" (or R2...) on a pad.</summary>
    public static string Use => Pad ? PadInput.Label(PadButton.RightTrigger) : "Click";
    /// <summary>The put-down / cancel verb: "Right-click" on a mouse, "B" (or Circle...) on a pad.</summary>
    public static string Cancel => Pad ? PadInput.Label(PadButton.East) : "Right-click";
    public static string Tools => PadInput.Label(PadButton.LeftShoulder) + " / " + PadInput.Label(PadButton.RightShoulder);
    public static string Zoom => Pad ? PadInput.Label(PadButton.LeftTrigger) + " / " + PadInput.Label(PadButton.RightTrigger) : "Scroll";
    public static string Orbit => Pad ? "Right stick" : "Middle-drag";
}
