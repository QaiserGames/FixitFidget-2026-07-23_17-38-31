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
    /// <summary>F / X. Retired by day in playtest 3 (stations are reach: nothing is stepped up to); at night it's the
    /// torch (Torch). Kept for anything that still names it.</summary>
    public static string Station => Pad ? PadInput.Label(PadButton.West) : "F";
    public static string Refuse => Pad ? PadInput.Label(PadButton.North) : "Q";
    public static string Back => Pad ? PadInput.Label(PadButton.East) : "Esc";
    public static string SwitchHand => Pad ? PadInput.Label(PadButton.RightShoulder) : "C";
    public static string View => Pad ? PadInput.Label(PadButton.Select) : "V";
    public static string Boost => Pad ? PadInput.Label(PadButton.LeftTrigger) : "Space";
    public static string LeftHand => Pad ? PadInput.Label(PadButton.LeftShoulder) : "Left click";
    public static string RightHand => Pad ? PadInput.Label(PadButton.RightShoulder) : "Right click";
    /// <summary>The "use / click" verb: "Click" on a mouse, "RT" (or R2...) on a pad. Also "Work on it": a device on the
    /// bench, in first person (playtest 3).</summary>
    public static string Use => Pad ? PadInput.Label(PadButton.RightTrigger) : "Click";
    /// <summary>The put-down / cancel verb: "Right-click" on a mouse, "B" (or Circle...) on a pad.</summary>
    public static string Cancel => Pad ? PadInput.Label(PadButton.East) : "Right-click";
    public static string Tools => PadInput.Label(PadButton.LeftShoulder) + " / " + PadInput.Label(PadButton.RightShoulder);
    public static string Zoom => Pad ? PadInput.Label(PadButton.LeftTrigger) + " / " + PadInput.Label(PadButton.RightTrigger) : "Scroll";
    public static string Orbit => Pad ? "Right stick" : "Middle-drag";
    /// <summary>Night walk: Ace's pocket torch. The day's station button, free at night (the café is closed).</summary>
    public static string Torch => Pad ? PadInput.Label(PadButton.West) : "F";
    /// <summary>Night walk: Ace's notebook page.</summary>
    public static string NotebookPage => Pad ? PadInput.Label(PadButton.DpadUp) : "N";
    /// <summary>Night walk: sneak (held Ctrl or C; the pad's left-stick click switches it on and off).</summary>
    public static string Sneak => Pad ? PadInput.Label(PadButton.LeftStickPress) : "Ctrl";
    /// <summary>A pad's aim steps (D-pad left / right, by day): to the next thing to use that side.</summary>
    public static string AimStep => Pad ? "D-pad" : "";
}
