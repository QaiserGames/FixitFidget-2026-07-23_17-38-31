#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE BENCH'S WORDS, CHECKED AT THEIR SOURCE (checklist v2, 14 Oct 2026; /root/cafe/specs/uiux-checklist-v2.md §6 A)
//
// His playtest 5: "the checklist is terrible"; the First Hour counted about 120 words on the first repair's screen; his
// decision 7: "Under 25 words on the bench screen". Fixit Fidget › Bench › Bench 6 counts what is on the screen while
// playing; this checks every source of those words in Edit Mode, with the keyboard's words and then the pad's (PadInput's
// device flag set for the check and put back, as the Day 1 onboarding check does), against WordBudget:
//   1. the job card's line, for every fault of every bench device (the phone, the pocket watch, Grace's camera), with the
//      longest customer name in the project's profiles: at most 8 words and 46 characters (a fault whose spoken form is
//      too long is named: his sister's shorter words go in FaultSpeech);
//   2. every step's verb at most 2 words (the bench's steps for each fault, built from the prefabs where Edit Mode allows,
//      and the card's own fallbacks); the flip line at most 4;
//   3. the first repair's controls card: 11 words with a mouse and with a pad, sticks swapped or not (the Producer's
//      call 6), each line at most 6;
//   4. the help hint at most 2; the finished line at most 6 with the longest name;
//   5. the bench's impatience line at most 12, in the bench's own keys (its back key, then E or A);
//   6. the intake tray: "Put it back" for a device lifted from it, "Put it on the intake tray" for any other, and on Day 1's
//      carry step it doesn't take the device at all;
//   7. the first repair's whole screen as the words add up (the card's line + its verb + the controls card + the hover + the
//      quiet corners) at most 25, and steady play's at most 24.
// Nothing in the scene, the project or the save changes (a device is made hidden for the steps and destroyed). The report
// goes to the Console and Logs/BenchWords/edit-check-<time>.txt.
// ---------------------------------------------------------------------------
public static class BenchWordsChecks
{
    const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Instances = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static readonly string[] BenchDevices =
    {
        "Assets/AssetsPrefabs/PhoneRepair.prefab",
        "Assets/AssetsPrefabs/PocketWatch.prefab",
        "Assets/GraceShowcase/GraceReunionCamera.prefab",
    };
    const string GraceCamera = "Assets/GraceShowcase/GraceReunionCamera.prefab";

    // The hover over the lens ("Lens") and the corners at the bench ("$0", "OPEN", "9:05 AM"): the spec's count (§5.1).
    const int HoverWords = 1, QuietCornerWords = 4, BlockedHoverWords = 6, LastOrdersCornerWords = 5;

    static readonly StringBuilder report = new StringBuilder();
    static int checks, failures, notes;

    [MenuItem("Fixit Fidget/Checks/Bench words")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Bench words] Stop Play mode first: this check reads the words in Edit mode.");
            return;
        }
        report.Clear();
        checks = failures = notes = 0;
        report.AppendLine("Bench words (checklist v2), edit-mode check");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        report.AppendLine();

        MethodInfo setPad = typeof(PadInput).GetProperty("UsingPad", Statics)?.GetSetMethod(true);
        bool padWas = PadInput.UsingPad, swapWas = GameSettings.SwapBenchSticks;
        if (setPad == null) Note("PadInput.UsingPad has no setter to reach: the pad's words are checked as whichever input is in use");
        string longest = LongestName(out string longestWords);
        Note($"the longest customer name: \"{longest}\" (the most words: \"{longestWords}\")");
        try
        {
            CardLines(longest);
            foreach (bool pad in new[] { false, true })
            {
                setPad?.Invoke(null, new object[] { pad });
                string input = pad ? "pad" : "mouse";
                report.AppendLine();
                report.AppendLine($"---------- {input} ----------");
                Verbs(input);
                Controls(input, false);
                if (pad)
                {
                    GameSettings.SwapBenchSticks = !swapWas;
                    Controls(input + ", sticks swapped", true);
                    GameSettings.SwapBenchSticks = swapWas;
                }
                Hint(input, longest);
                Impatience(input, longest);
                Budgets(input, longest);
            }
            report.AppendLine();
            report.AppendLine("---------- the steps of every bench job ----------");
            Steps();
            report.AppendLine();
            report.AppendLine("---------- the intake tray ----------");
            Tray();
        }
        catch (Exception e)
        {
            Check(false, "the check ran to its end (it stopped: " + e.Message + ")");
            Debug.LogException(e);
        }
        finally
        {
            setPad?.Invoke(null, new object[] { padWas });
            if (GameSettings.SwapBenchSticks != swapWas) GameSettings.SwapBenchSticks = swapWas;   // the player's setting, never the check's
        }

        report.AppendLine();
        report.AppendLine($"{checks} checks, {failures} failed, {notes} notes.");
        string text = report.ToString();
        try
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "BenchWords"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, $"edit-check-{DateTime.Now:yyyy-MM-dd_HHmmss}.txt"), text);
        }
        catch (Exception e) { Debug.LogWarning("[Bench words] Could not write the report: " + e.Message); }
        if (failures == 0) Debug.Log($"[Bench words] PASS: all {checks} checks.\n{text}");
        else Debug.LogError($"[Bench words] {failures} of {checks} checks FAILED.\n{text}");
    }

    // ------------------------------------------------------------------ 1. the card's line

    static void CardLines(string longest)
    {
        report.AppendLine("---------- the job card's line (every fault of every bench device, the longest name) ----------");
        foreach (string path in BenchDevices)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            DeviceDefinition definition = prefab != null ? prefab.GetComponent<DeviceDefinition>() : null;
            if (definition == null || definition.faults == null) { Check(false, $"{path} loads with its faults"); continue; }
            for (int i = 0; i < definition.faults.Length; i++)
            {
                DeviceFault fault = definition.faults[i];
                if (fault == null) continue;
                bool spoken = FaultSpeech.TryGet(fault.description, out _);
                string line = BenchCards.CardLine(longest, fault.description);
                int words = WordBudget.Words(line);
                Check(words <= WordBudget.CardLineWords && line.Length <= WordBudget.CardLineChars,
                    $"{Path.GetFileNameWithoutExtension(path)} fault {i} (\"{fault.description}\"): {line}  ({words} words, {line.Length} characters; " +
                    $"the rule is {WordBudget.CardLineWords} and {WordBudget.CardLineChars}{(spoken ? "" : "; NO spoken form in FaultSpeech: said as its label")})");
            }
        }
    }

    // ------------------------------------------------------------------ 2. the verbs

    static void Verbs(string input)
    {
        foreach (ToolType tool in (ToolType[])Enum.GetValues(typeof(ToolType)))
        {
            string verb = BenchCards.VerbOf(new RepairChecklist.Step { tool = tool });
            Check(WordBudget.Words(verb) <= WordBudget.BenchVerb, $"{input}: the card's own verb for the {tool} (when a step has none): \"{verb}\"");
        }
        // 19 Oct: a key is an icon (a sprite tag) found in the rich text through the helper the card uses, never a bracketed
        // word; the plain text (the tags out) is what the word count reads.
        string flipRich = BenchCards.FlipWords(), flip = BenchCards.Plain(flipRich);
        Check(WordBudget.Words(flip) <= WordBudget.BenchFlip && flipRich.Contains(ControlHints.Flip) && !flipRich.Contains("[") && flip.Contains("Flip it over"),
            $"{input}: the flip, with its key as an icon: \"{flip}\" ({WordBudget.Report(flip, WordBudget.BenchFlip)})");
    }

    // ------------------------------------------------------------------ 3. the controls card

    static void Controls(string input, bool swapped)
    {
        string card = BenchCards.FirstRepairControlsWords();
        string plain = BenchCards.Plain(card);
        int words = WordBudget.Words(plain);
        Check(words <= WordBudget.FirstRepairControls,
            $"{input}: the first repair's controls card, {WordBudget.Report(plain, WordBudget.FirstRepairControls)}: \"{plain}\"");
        foreach (string line in card.Split('\n'))
        {
            string one = BenchCards.Plain(line);
            Check(WordBudget.Words(one) <= WordBudget.ControlsLine, $"{input}: a line of it, \"{one}\" ({WordBudget.Report(one, WordBudget.ControlsLine)})");
        }
        Check(card.Contains(ControlHints.BenchHelp) && plain.Contains("What's left") && !plain.Contains("hold: controls") && !card.Contains("["),
            $"{input}: it names the help key (an icon, no bracket) for what's left, and leaves \"(hold: controls)\" to the held card");
        if (ControlHints.Pad)
            Check(card.Contains(ControlHints.BenchHandStick), $"{input}: the hand's stick follows the swap setting ({BenchCards.Plain(ControlHints.BenchHandStick)})");
        else
            Check(card.Contains(ControlHints.Use) && plain.Contains("Drag a tool") && !plain.Contains("caddy") && !plain.Contains("Click a tool"),
                $"{input}: a tool is taken by dragging it with the mouse's button (direct grab, 14 Oct), not \"click it in the caddy\"");
        string heldRich = BenchCards.HeldControlsWords(), held = BenchCards.Plain(heldRich);
        Check(held.Contains("hold: controls") && held.Contains("Pick it up") && held.Contains("Flip it over") && heldRich.Contains(ControlHints.Flip) && heldRich.Contains(ControlHints.TakeFromBench) && !heldRich.Contains("["),
            $"{input}: the held card has every control (no budget), its keys as icons: \"{held}\"");
        if (swapped) Note($"{input}: checked with the sticks swapped, put back after");
    }

    // ------------------------------------------------------------------ 4. the hint and the finished line

    static void Hint(string input, string longest)
    {
        string hintRich = BenchCards.HintWords(), hint = BenchCards.Plain(hintRich);
        Check(WordBudget.Words(hint) <= WordBudget.HelpHint && hintRich.Contains(ControlHints.BenchHelp) && !hintRich.Contains("["),
            $"{input}: the help hint \"{hint}\", its key an icon ({WordBudget.Report(hint, WordBudget.HelpHint)})");
        foreach (string who in new[] { longest, "the Officer" })
        {
            string doneRich = BenchCards.FinishedWords(who, false), done = BenchCards.Plain(doneRich);
            Check(WordBudget.Words(done) <= WordBudget.FinishedRow && doneRich.Contains(ControlHints.TakeFromBench) && !doneRich.Contains("[") && done.Contains("Take it to"),
                $"{input}: the finished line \"{done}\", its key an icon ({WordBudget.Report(done, WordBudget.FinishedRow)})");
        }
        string full = BenchCards.Plain(BenchCards.FinishedWords(longest, true)), nobody = BenchCards.Plain(BenchCards.FinishedWords("", false));
        Check(WordBudget.Words(full) <= WordBudget.FinishedRow && WordBudget.Words(nobody) <= WordBudget.FinishedRow,
            $"{input}: hands full \"{full}\"; no owner \"{nobody}\"");
    }

    // ------------------------------------------------------------------ 5. the impatience card at the bench

    static void Impatience(string input, string longest)
    {
        string line = DayOneGuideUI.BenchReassureWords(longest), plainLine = BenchCards.Plain(line);
        Check(WordBudget.Words(plainLine) <= WordBudget.BenchAlert
              && line.Contains(ControlHints.Cancel) && line.Contains(ControlHints.Interact) && !plainLine.Contains("press E"),
            $"{input}: the bench's impatience line, in the bench's keys (through ControlHints): \"{plainLine}\" ({WordBudget.Report(plainLine, WordBudget.BenchAlert)})");
    }

    // ------------------------------------------------------------------ 7. the screens, added up

    static void Budgets(string input, string longest)
    {
        // The first repair: Grace's camera on Day 1 (its fault 0), the cloth not taken, the cursor on the lens.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GraceCamera);
        DeviceDefinition camera = prefab != null ? prefab.GetComponent<DeviceDefinition>() : null;
        DeviceFault clean = camera != null ? camera.GetFault(0) : null;
        if (clean == null) { Check(false, $"{GraceCamera} has a fault 0 (Day 1's clean)"); return; }
        string line = BenchCards.CardLine("Grace", clean.description);
        string verb = BenchCards.VerbOf(new RepairChecklist.Step { tool = ToolType.Cloth });
        int controls = WordBudget.Words(BenchCards.Plain(BenchCards.FirstRepairControlsWords()));
        int first = WordBudget.Words(line) + WordBudget.Words(verb) + controls + HoverWords + QuietCornerWords;
        Check(first <= WordBudget.BenchFirstRepair,
            $"{input}: the first repair's screen adds up to {first} words (the rule is {WordBudget.BenchFirstRepair}): the line {WordBudget.Words(line)} " +
            $"(\"{line}\"), \"{verb}\" {WordBudget.Words(verb)}, the controls card {controls}, the hover {HoverWords}, the quiet corners {QuietCornerWords}");

        // Steady play: the longest line any bench fault makes, a two-word verb, the hint, a hovered part, the corners; and
        // the worst case: a flip, a blocked part's own "why not" hovered, last orders.
        int hint = WordBudget.Words(BenchCards.Plain(BenchCards.HintWords()));
        int flip = WordBudget.Words(BenchCards.Plain(BenchCards.FlipWords()));
        int longestLine = 0, worst = 0;
        foreach (string path in BenchDevices)
        {
            DeviceDefinition d = AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<DeviceDefinition>();
            if (d == null || d.faults == null) continue;
            int deviceLine = 0, openedLine = 0;
            foreach (DeviceFault f in d.faults)
            {
                if (f == null) continue;
                int n = WordBudget.Words(BenchCards.CardLine(longest, f.description));
                deviceLine = Mathf.Max(deviceLine, n);
                // Only a fault worked with the device opened (Mechanical, Cleaning) can put a covered part under the cursor;
                // the Human fault is a ten-second breather that never opens it (lock), software and bureaucracy never do.
                if (f.type == FaultType.Mechanical || f.type == FaultType.Cleaning) openedLine = Mathf.Max(openedLine, n);
            }
            longestLine = Mathf.Max(longestLine, deviceLine);
            // A blocked part's own "why not" ("Under the back cover") can be hovered only on a device with a cover over
            // its work (the phone, the watch) and in a fault that opens it; Grace's camera has no cover (review m2).
            bool covers = d.GetComponentsInChildren<RemovablePart>(true).Length > 0;
            worst = Mathf.Max(worst, deviceLine + flip + hint + HoverWords + LastOrdersCornerWords);
            if (covers) worst = Mathf.Max(worst, openedLine + flip + hint + BlockedHoverWords + LastOrdersCornerWords);
        }
        int steady = longestLine + WordBudget.BenchVerb + hint + HoverWords + QuietCornerWords;
        Check(steady <= WordBudget.BenchSteady && worst <= WordBudget.BenchSteady,
            $"{input}: steady play adds up to {steady} (aim {WordBudget.BenchSteadyAim}), the worst case to {worst} (the rule is {WordBudget.BenchSteady})");
        if (steady > WordBudget.BenchSteadyAim) Note($"{input}: steady play is over the aim of {WordBudget.BenchSteadyAim} with the longest line ({longestLine} words)");
    }

    // ------------------------------------------------------------------ 2b. every bench job's steps

    // Each bench device, each fault, made hidden in Edit mode with that fault applied: the checklist's steps for it, each
    // verb at most 2 words and its icon in the project. In Edit mode no part has run its Awake, so a device whose steps can't
    // be worked out here is a note (Bench 6 and Bench 3 see the steps while playing), not a failure.
    static void Steps()
    {
        var steps = new List<RepairChecklist.Step>();
        var missing = new HashSet<string>();
        foreach (string path in BenchDevices)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            DeviceDefinition definition = prefab != null ? prefab.GetComponent<DeviceDefinition>() : null;
            if (definition == null || definition.faults == null) continue;
            for (int i = 0; i < definition.faults.Length; i++)
            {
                GameObject device = null;
                try
                {
                    device = UnityEngine.Object.Instantiate(prefab);
                    device.hideFlags = HideFlags.HideAndDontSave;
                    device.transform.position = new Vector3(0f, -500f, 0f);
                    DeviceDefinition d = device.GetComponent<DeviceDefinition>();
                    JobBase job = device.GetComponent<JobBase>();
                    if (d == null || job == null) { Note($"{path}: no job on it to list the steps of"); break; }
                    d.ApplyFault(i);
                    DeviceFault fault = d.GetFault(i);
                    job.Configure(new Job
                    {
                        kind = JobKind.Repair, devicePrefab = prefab, deviceName = d.displayName, faultIndex = i,
                        faultType = fault.type, faultDescription = fault.description, payout = fault.payout, number = 1
                    });
                    RepairChecklist.Build(job, steps);
                    var line = new StringBuilder();
                    foreach (RepairChecklist.Step step in steps)
                    {
                        string verb = BenchCards.VerbOf(step), icon = BenchCards.IconOf(step);
                        if (!BenchCards.HasIcon(icon)) missing.Add(icon);
                        line.Append(line.Length > 0 ? " · " : "").Append(icon).Append(": ").Append(step.under ? BenchCards.Plain(BenchCards.FlipWords()) : verb);
                        Check(WordBudget.Words(verb) <= WordBudget.BenchVerb, $"{Path.GetFileNameWithoutExtension(path)} fault {i}: the verb \"{verb}\" ({icon})");
                    }
                    Note($"{Path.GetFileNameWithoutExtension(path)} fault {i} (\"{fault.description}\"): {steps.Count} icons: {line}");
                    if (steps.Count > 7) Note($"...more than 7 icons: the card shows the first 8 (MostIcons)");
                }
                catch (Exception e)
                {
                    Note($"{Path.GetFileNameWithoutExtension(path)} fault {i}: its steps can't be worked out in Edit mode ({e.GetType().Name}: {e.Message}); Bench 6 sees them in play");
                }
                finally
                {
                    if (device != null) UnityEngine.Object.DestroyImmediate(device);
                }
            }
        }
        if (missing.Count > 0) Note("icons not in Resources/UI/BenchIcons yet (a faint disc shows instead; the verb and the glow carry the step): " + string.Join(", ", missing));
        else Note("every icon the steps use is in Resources/UI/BenchIcons");
    }

    // ------------------------------------------------------------------ 6. the intake tray

    static void Tray()
    {
        Check(DropInteractable.TrayWords(DropSpot.SpotKind.Counter, true, true) == DropInteractable.PutBackWords,
            $"a device lifted from the intake tray: \"{DropInteractable.TrayWords(DropSpot.SpotKind.Counter, true, true)}\"");
        Check(DropInteractable.TrayWords(DropSpot.SpotKind.Counter, true, false) == DropInteractable.IntakeTrayWords,
            $"any other device at the tray: \"{DropInteractable.TrayWords(DropSpot.SpotKind.Counter, true, false)}\"");
        Check(DropInteractable.TrayWords(DropSpot.SpotKind.Bench, true, true) == "Set down" && DropInteractable.TrayWords(DropSpot.SpotKind.Counter, false, true) == "No room here",
            "the bench says \"Set down\", a full tray \"No room here\"");

        // Day 1's carry step: the tray doesn't take the device the card is sending to the bench (the bench still does).
        var host = new GameObject("Bench words check (temporary)") { hideFlags = HideFlags.HideAndDontSave };
        host.SetActive(false);
        MethodInfo setCarrying = typeof(DayOneGuideUI).GetProperty("CarryingToBench", Statics)?.GetSetMethod(true);
        try
        {
            DropSpot counter = host.AddComponent<DropSpot>(), bench = new GameObject("Bench spot (temporary)") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<DropSpot>();
            bench.transform.SetParent(host.transform, false);
            FieldInfo kind = typeof(DropSpot).GetField("kind", Instances);
            if (kind == null || setCarrying == null) { Note("DropSpot.kind or DayOneGuideUI.CarryingToBench can't be reached here: Day 1's carry step is the Day 1 guide lab's"); return; }
            kind.SetValue(counter, DropSpot.SpotKind.Counter);
            kind.SetValue(bench, DropSpot.SpotKind.Bench);
            JobBase device = host.AddComponent<RepairJob>();
            setCarrying.Invoke(null, new object[] { device });
            Check(!DropInteractable.Takes(counter, device) && DropInteractable.Takes(bench, device) && DropInteractable.TrayWords(counter, device) == "",
                "on Day 1's carry step the intake tray doesn't offer to take the device back (no words, no E); the bench does");
            setCarrying.Invoke(null, new object[] { null });
            Check(DropInteractable.Takes(counter, device), "...and once the card moves on, the tray takes it again");
        }
        finally
        {
            setCarrying?.Invoke(null, new object[] { null });
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    // ------------------------------------------------------------------ helpers

    // The longest name a customer can have: every profile's character name (the regulars and the walk-ins' profiles).
    static string LongestName(out string mostWords)
    {
        string longest = "Grace";
        mostWords = "Grace";
        foreach (string guid in AssetDatabase.FindAssets("t:CustomerProfile"))
        {
            CustomerProfile profile = AssetDatabase.LoadAssetAtPath<CustomerProfile>(AssetDatabase.GUIDToAssetPath(guid));
            string name = profile != null ? profile.characterName : null;
            if (string.IsNullOrWhiteSpace(name)) continue;
            name = name.Trim();
            if (name.Length > longest.Length) longest = name;
            if (WordBudget.Words(name) > WordBudget.Words(mostWords)) mostWords = name;
        }
        return WordBudget.Words(mostWords) > WordBudget.Words(longest) ? mostWords : longest;
    }

    static void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine($"{(ok ? "PASS" : "FAIL")}  {what}");
    }

    static void Note(string what)
    {
        notes++;
        report.AppendLine($"note  {what}");
    }
}
#endif
