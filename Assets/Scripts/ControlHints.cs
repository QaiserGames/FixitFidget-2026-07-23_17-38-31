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
    /// <summary>Turn the device over on the bench's mat (the tabletop, 8 Oct).</summary>
    public static string Flip => Pad ? PadInput.Label(PadButton.North) : "R";
    public static string Orbit => Pad ? "Right stick" : "Middle-drag";
    /// <summary>Night walk: Ace's pocket torch. The day's station button, free at night (the café is closed).</summary>
    public static string Torch => Pad ? PadInput.Label(PadButton.West) : "F";
    /// <summary>Night walk: Ace's notebook page.</summary>
    public static string NotebookPage => Pad ? PadInput.Label(PadButton.DpadUp) : "N";
    /// <summary>Night walk: sneak (held Ctrl or C; the pad's left-stick click switches it on and off).</summary>
    public static string Sneak => Pad ? PadInput.Label(PadButton.LeftStickPress) : "Ctrl";
    /// <summary>A pad's aim steps (D-pad left / right, by day): to the next thing to use that side.</summary>
    public static string AimStep => Pad ? "D-pad" : "";
    /// <summary>The phone and the pause (PausePhone, playtest 3 session 3): Esc, or the pad's Start / Options / Plus.</summary>
    public static string Pause => Pad ? PadInput.Label(PadButton.Start) : "Esc";
    /// <summary>At the bench: pick the device up and step back. E, or X on a pad (12 Oct: A uses there).</summary>
    public static string TakeFromBench => Pad ? PadInput.Label(PadButton.West) : "E";
    /// <summary>At the bench: use the tool in hand. Click, or A / RT on a pad (12 Oct).</summary>
    public static string BenchUse => Pad ? PadInput.Label(PadButton.South) + " / " + PadInput.Label(PadButton.RightTrigger) : "Hold click";
    /// <summary>At the bench: zoom. The scroll wheel, or the D-pad up and down.</summary>
    public static string BenchZoom => Pad ? "D-pad up/down" : "Scroll";
    /// <summary>At the bench: the full controls card again, held (BenchCards). Tab, or View / Select on a pad.</summary>
    public static string BenchHelp => Pad ? PadInput.Label(PadButton.Select) : "Tab";
    /// <summary>
    /// At the bench: the stick that moves the hand (the pad's cursor). The right one, as everywhere else the right stick
    /// aims; the left one with the sticks swapped (GameSettings.SwapBenchSticks, 12 Oct: "a setting to swap the sticks").
    /// </summary>
    public static string BenchHandStick => GameSettings.SwapBenchSticks ? "Left stick" : "Right stick";
    /// <summary>At the bench: the stick that turns the device a quarter at a time (and lifts an edge to peek under). The
    /// left one; the right one with the sticks swapped.</summary>
    public static string BenchTurnStick => GameSettings.SwapBenchSticks ? "Right stick" : "Left stick";
}
