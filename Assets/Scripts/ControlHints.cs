/// <summary>
/// Button icons for on-screen prompts. Each verb follows the device the player used last: a keycap or a mouse icon by
/// default, the pad's own button once a controller is being used. See PadInput for the full controller layout.
///
/// ICONS, NOT WORDS (19 Oct 2026, playtest 7). His girlfriend is dyslexic and plays on a PlayStation pad: "Instead of
/// writing out square, just put the icon." "your text needs to be bolder." Every verb here is a TextMesh Pro sprite tag
/// now (`&lt;sprite name="ps_cross"&gt;`, `&lt;sprite name="key_e"&gt;`), from the icon sheet (Tools/UI/draw_button_icons.py;
/// Fixit Fidget › UI › Button icons). The contract for every screen:
///   * write icon + space + verb: ControlHints.Interact + " Talk to Grace"; never "[" + ... + "]" round a label, and never
///     "Press X" with a button's name spelled out;
///   * the text must be rich text (TextMesh Pro's default; RepairOverlayUI turns it off: turn it back on for a line with
///     an icon);
///   * Word(Verb) (and Words.Interact...) give the old plain word for logs and labs; a lab compares against these helpers,
///     never against literal button words.
/// Where the sheet isn't there (or lacks a name) every helper gives the word instead (PadInput.Icon), so a raw tag never
/// shows.
/// </summary>
public static class ControlHints
{
    public static bool Pad => PadInput.UsingPad;

    /// <summary>Picks the keyboard-and-mouse or the controller wording.</summary>
    public static string Say(string keyboardAndMouse, string controller) => Pad ? controller : keyboardAndMouse;

    /// <summary>The verbs, for Word(verb) and Icon(verb).</summary>
    public enum Verb
    {
        Interact, Station, Refuse, Back, SwitchHand, View, Boost, LeftHand, RightHand, Use, Cancel, Tools, Zoom, Flip, Orbit,
        Torch, NotebookPage, Sneak, AimStep, Pause, TakeFromBench, BenchUse, BenchZoom, BenchHelp, BenchHandStick, BenchTurnStick,
        Walk, Choose, Confirm, StopNeedle,
    }

    // ---------- the icons (what the screens show) ----------

    public static string Interact => Icon(Verb.Interact);
    /// <summary>F / X. Retired by day in playtest 3 (stations are reach: nothing is stepped up to); at night it's the
    /// torch (Torch). Kept for anything that still names it.</summary>
    public static string Station => Icon(Verb.Station);
    public static string Refuse => Icon(Verb.Refuse);
    public static string Back => Icon(Verb.Back);
    public static string SwitchHand => Icon(Verb.SwitchHand);
    public static string View => Icon(Verb.View);
    public static string Boost => Icon(Verb.Boost);
    public static string LeftHand => Icon(Verb.LeftHand);
    public static string RightHand => Icon(Verb.RightHand);
    /// <summary>The "use / click" verb: the left mouse button, R2 (RT, ZR) on a pad. Also "Work on it": a device on the
    /// bench, in first person (playtest 3).</summary>
    public static string Use => Icon(Verb.Use);
    /// <summary>The put-down / cancel verb: the right mouse button, Circle (B, A) on a pad.</summary>
    public static string Cancel => Icon(Verb.Cancel);
    public static string Tools => Icon(Verb.Tools);
    public static string Zoom => Icon(Verb.Zoom);
    /// <summary>Turn the device over on the bench's mat (the tabletop, 8 Oct).</summary>
    public static string Flip => Icon(Verb.Flip);
    public static string Orbit => Icon(Verb.Orbit);
    /// <summary>Night walk: Ace's pocket torch. The day's station button, free at night (the café is closed).</summary>
    public static string Torch => Icon(Verb.Torch);
    /// <summary>Night walk: Ace's notebook page.</summary>
    public static string NotebookPage => Icon(Verb.NotebookPage);
    /// <summary>Night walk: sneak (held Ctrl or C; the pad's left-stick click switches it on and off).</summary>
    public static string Sneak => Icon(Verb.Sneak);
    /// <summary>A pad's aim steps (D-pad left / right, by day): to the next thing to use that side.</summary>
    public static string AimStep => Icon(Verb.AimStep);
    /// <summary>The phone and the pause (PausePhone, playtest 3 session 3): Esc, or the pad's Start / Options / Plus.</summary>
    public static string Pause => Icon(Verb.Pause);
    /// <summary>At the bench: pick the device up and step back. E, or X on a pad (12 Oct: A uses there).</summary>
    public static string TakeFromBench => Icon(Verb.TakeFromBench);
    /// <summary>At the bench: use the tool in hand. Hold the left mouse button, or A / RT on a pad (12 Oct).</summary>
    public static string BenchUse => Icon(Verb.BenchUse);
    /// <summary>At the bench: zoom. The scroll wheel, or the D-pad up and down.</summary>
    public static string BenchZoom => Icon(Verb.BenchZoom);
    /// <summary>At the bench: the full controls card again, held (BenchCards). Tab, or View / Select on a pad.</summary>
    public static string BenchHelp => Icon(Verb.BenchHelp);
    /// <summary>
    /// At the bench: the stick that moves the hand (the pad's cursor). The right one, as everywhere else the right stick
    /// aims; the left one with the sticks swapped (GameSettings.SwapBenchSticks, 12 Oct: "a setting to swap the sticks").
    /// </summary>
    public static string BenchHandStick => Icon(Verb.BenchHandStick);
    /// <summary>At the bench: the stick that turns the device a quarter at a time (and lifts an edge to peek under). The
    /// left one; the right one with the sticks swapped.</summary>
    public static string BenchTurnStick => Icon(Verb.BenchTurnStick);
    /// <summary>Walking: the left stick, or W A S D.</summary>
    public static string Walk => Icon(Verb.Walk);
    /// <summary>Choosing a reply: the D-pad up and down, or W and S.</summary>
    public static string Choose => Icon(Verb.Choose);
    /// <summary>"Go on": Cross (A, B) on a pad, Enter on a keyboard (the day intro's skip).</summary>
    public static string Confirm => Icon(Verb.Confirm);
    /// <summary>The straight-face meter's stop: Square (X, Y) on a pad, Space on a keyboard (MorningFace).</summary>
    public static string StopNeedle => Icon(Verb.StopNeedle);

    /// <summary>The icon for <paramref name="verb"/> on the device in hand (a sprite tag; the word where there is no icon).</summary>
    public static string Icon(Verb verb)
    {
        switch (verb)
        {
            case Verb.Interact: return Pad ? PadInput.Label(PadButton.South) : Key("E");
            case Verb.Station: return Pad ? PadInput.Label(PadButton.West) : Key("F");
            case Verb.Refuse: return Pad ? PadInput.Label(PadButton.North) : Key("Q");
            case Verb.Back: return Pad ? PadInput.Label(PadButton.East) : Key("Esc");
            case Verb.SwitchHand: return Pad ? PadInput.Label(PadButton.RightShoulder) : Key("C");
            case Verb.View: return Pad ? PadInput.Label(PadButton.Select) : Key("V");
            case Verb.Boost: return Pad ? PadInput.Label(PadButton.LeftTrigger) : Key("Space");
            case Verb.LeftHand: return Pad ? PadInput.Label(PadButton.LeftShoulder) : Key("Left click");
            case Verb.RightHand: return Pad ? PadInput.Label(PadButton.RightShoulder) : Key("Right click");
            case Verb.Use: return Pad ? PadInput.Label(PadButton.RightTrigger) : Key("Click");
            case Verb.Cancel: return Pad ? PadInput.Label(PadButton.East) : Key("Right-click");
            case Verb.Tools: return PadInput.Label(PadButton.LeftShoulder) + " / " + PadInput.Label(PadButton.RightShoulder);
            case Verb.Zoom: return Pad ? PadInput.Label(PadButton.LeftTrigger) + " / " + PadInput.Label(PadButton.RightTrigger) : Key("Scroll");
            case Verb.Flip: return Pad ? PadInput.Label(PadButton.North) : Key("R");
            case Verb.Orbit: return Pad ? PadInput.Icon("rs", "Right stick") : Key("Middle-drag");
            case Verb.Torch: return Pad ? PadInput.Label(PadButton.West) : Key("F");
            case Verb.NotebookPage: return Pad ? PadInput.Label(PadButton.DpadUp) : Key("N");
            case Verb.Sneak: return Pad ? PadInput.Label(PadButton.LeftStickPress) : Key("Ctrl");
            case Verb.AimStep: return Pad ? PadInput.Icon("dpad_leftright", "D-pad") : "";
            case Verb.Pause: return Pad ? PadInput.Label(PadButton.Start) : Key("Esc");
            case Verb.TakeFromBench: return Pad ? PadInput.Label(PadButton.West) : Key("E");
            case Verb.BenchUse: return Pad ? PadInput.Label(PadButton.South) + " / " + PadInput.Label(PadButton.RightTrigger) : Key("Hold click");
            case Verb.BenchZoom: return Pad ? PadInput.Icon("dpad_updown", "D-pad up/down") : Key("Scroll");
            case Verb.BenchHelp: return Pad ? PadInput.Label(PadButton.Select) : Key("Tab");
            case Verb.BenchHandStick: return GameSettings.SwapBenchSticks ? PadInput.Icon("ls", "Left stick") : PadInput.Icon("rs", "Right stick");
            case Verb.BenchTurnStick: return GameSettings.SwapBenchSticks ? PadInput.Icon("rs", "Right stick") : PadInput.Icon("ls", "Left stick");
            case Verb.Walk: return Pad ? PadInput.Icon("ls", "Left stick") : Key("WASD");
            case Verb.Choose: return Pad ? PadInput.Icon("dpad_updown", "D-pad") : Key("W/S");
            case Verb.Confirm: return Pad ? PadInput.Label(PadButton.South) : Key("Enter");
            case Verb.StopNeedle: return Pad ? PadInput.Label(PadButton.West) : Key("Space");
            default: return "";
        }
    }

    // ---------- the words (logs, labs, the fallback) ----------

    /// <summary>The plain word for <paramref name="verb"/> on the device in hand ("E", "Cross", "Right stick"): logs and labs.</summary>
    public static string Word(Verb verb)
    {
        switch (verb)
        {
            case Verb.Interact: return Pad ? PadInput.Word(PadButton.South) : "E";
            case Verb.Station: return Pad ? PadInput.Word(PadButton.West) : "F";
            case Verb.Refuse: return Pad ? PadInput.Word(PadButton.North) : "Q";
            case Verb.Back: return Pad ? PadInput.Word(PadButton.East) : "Esc";
            case Verb.SwitchHand: return Pad ? PadInput.Word(PadButton.RightShoulder) : "C";
            case Verb.View: return Pad ? PadInput.Word(PadButton.Select) : "V";
            case Verb.Boost: return Pad ? PadInput.Word(PadButton.LeftTrigger) : "Space";
            case Verb.LeftHand: return Pad ? PadInput.Word(PadButton.LeftShoulder) : "Left click";
            case Verb.RightHand: return Pad ? PadInput.Word(PadButton.RightShoulder) : "Right click";
            case Verb.Use: return Pad ? PadInput.Word(PadButton.RightTrigger) : "Click";
            case Verb.Cancel: return Pad ? PadInput.Word(PadButton.East) : "Right-click";
            case Verb.Tools: return PadInput.Word(PadButton.LeftShoulder) + " / " + PadInput.Word(PadButton.RightShoulder);
            case Verb.Zoom: return Pad ? PadInput.Word(PadButton.LeftTrigger) + " / " + PadInput.Word(PadButton.RightTrigger) : "Scroll";
            case Verb.Flip: return Pad ? PadInput.Word(PadButton.North) : "R";
            case Verb.Orbit: return Pad ? "Right stick" : "Middle-drag";
            case Verb.Torch: return Pad ? PadInput.Word(PadButton.West) : "F";
            case Verb.NotebookPage: return Pad ? PadInput.Word(PadButton.DpadUp) : "N";
            case Verb.Sneak: return Pad ? PadInput.Word(PadButton.LeftStickPress) : "Ctrl";
            case Verb.AimStep: return Pad ? "D-pad" : "";
            case Verb.Pause: return Pad ? PadInput.Word(PadButton.Start) : "Esc";
            case Verb.TakeFromBench: return Pad ? PadInput.Word(PadButton.West) : "E";
            case Verb.BenchUse: return Pad ? PadInput.Word(PadButton.South) + " / " + PadInput.Word(PadButton.RightTrigger) : "Hold click";
            case Verb.BenchZoom: return Pad ? "D-pad up/down" : "Scroll";
            case Verb.BenchHelp: return Pad ? PadInput.Word(PadButton.Select) : "Tab";
            case Verb.BenchHandStick: return GameSettings.SwapBenchSticks ? "Left stick" : "Right stick";
            case Verb.BenchTurnStick: return GameSettings.SwapBenchSticks ? "Right stick" : "Left stick";
            case Verb.Walk: return Pad ? "Left stick" : "WASD";
            case Verb.Choose: return Pad ? "D-pad" : "W/S";
            case Verb.Confirm: return Pad ? PadInput.Word(PadButton.South) : "Enter";
            case Verb.StopNeedle: return Pad ? PadInput.Word(PadButton.West) : "Space";
            default: return "";
        }
    }

    /// <summary>The words by name (ControlHints.Words.Interact is Word(Verb.Interact)): logs and labs.</summary>
    public static class Words
    {
        public static string Interact => Word(Verb.Interact);
        public static string Station => Word(Verb.Station);
        public static string Refuse => Word(Verb.Refuse);
        public static string Back => Word(Verb.Back);
        public static string SwitchHand => Word(Verb.SwitchHand);
        public static string View => Word(Verb.View);
        public static string Boost => Word(Verb.Boost);
        public static string LeftHand => Word(Verb.LeftHand);
        public static string RightHand => Word(Verb.RightHand);
        public static string Use => Word(Verb.Use);
        public static string Cancel => Word(Verb.Cancel);
        public static string Tools => Word(Verb.Tools);
        public static string Zoom => Word(Verb.Zoom);
        public static string Flip => Word(Verb.Flip);
        public static string Orbit => Word(Verb.Orbit);
        public static string Torch => Word(Verb.Torch);
        public static string NotebookPage => Word(Verb.NotebookPage);
        public static string Sneak => Word(Verb.Sneak);
        public static string AimStep => Word(Verb.AimStep);
        public static string Pause => Word(Verb.Pause);
        public static string TakeFromBench => Word(Verb.TakeFromBench);
        public static string BenchUse => Word(Verb.BenchUse);
        public static string BenchZoom => Word(Verb.BenchZoom);
        public static string BenchHelp => Word(Verb.BenchHelp);
        public static string BenchHandStick => Word(Verb.BenchHandStick);
        public static string BenchTurnStick => Word(Verb.BenchTurnStick);
        public static string Walk => Word(Verb.Walk);
        public static string Choose => Word(Verb.Choose);
        public static string Confirm => Word(Verb.Confirm);
        public static string StopNeedle => Word(Verb.StopNeedle);
    }

    // ---------- keyboard and mouse ----------

    /// <summary>
    /// A keyboard or mouse word as its icon: a letter or digit as its keycap (key_e), Esc / Tab / Space / Ctrl / Shift /
    /// Enter and the arrows as theirs, "Click" and "Left click" the left mouse button, "Right-click" the right one,
    /// "Middle-drag" the middle, "Scroll" the wheel, "Hold click" the left button + " hold", "WASD" the four keycaps, "W/S"
    /// two. The word itself where the sheet has no icon for it.
    /// </summary>
    public static string Key(string word)
    {
        if (string.IsNullOrEmpty(word)) return "";
        switch (word)
        {
            case "Click":
            case "Left click": return PadInput.Icon("mouse_left", word);
            case "Right-click":
            case "Right click": return PadInput.Icon("mouse_right", word);
            case "Middle-drag": return PadInput.Icon("mouse_middle", word);
            case "Scroll": return PadInput.Icon("mouse_wheel", word);
            case "Hold click": return PadInput.IconsAvailable ? PadInput.Icon("mouse_left", word) + " hold" : word;
            case "WASD": return PadInput.IconsAvailable
                ? PadInput.Icon("key_w", "W") + PadInput.Icon("key_a", "A") + PadInput.Icon("key_s", "S") + PadInput.Icon("key_d", "D") : word;
            case "W/S": return PadInput.IconsAvailable ? PadInput.Icon("key_w", "W") + PadInput.Icon("key_s", "S") : word;
            case "Esc": return PadInput.Icon("key_esc", word);
            case "Tab": return PadInput.Icon("key_tab", word);
            case "Space": return PadInput.Icon("key_space", word);
            case "Ctrl": return PadInput.Icon("key_ctrl", word);
            case "Shift": return PadInput.Icon("key_shift", word);
            case "Enter": return PadInput.Icon("key_enter", word);
            case "Up": return PadInput.Icon("key_up", word);
            case "Down": return PadInput.Icon("key_down", word);
            case "Left": return PadInput.Icon("key_left", word);
            case "Right": return PadInput.Icon("key_right", word);
        }
        if (word.Length == 1 && (char.IsLetter(word[0]) || char.IsDigit(word[0])))
            return PadInput.Icon("key_" + char.ToLowerInvariant(word[0]), word);
        return word;
    }
}
