#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// BARKS (claude/foundation-pass-build-plan.md §5, agreed 5 Oct 2026)
//
//   Fixit Fidget > Night > Barks 1 - Make or update the night lines (placeholders)
//   Fixit Fidget > Night > Barks 2 - Check barks (lab, Play Mode)
//   Fixit Fidget > Checks > Bark rules (BarkRuleChecks)
//
// Barks 1 makes Assets/Data/Resources/Night lines.asset, or adds to it what's missing: the night's cast
// (the man, Ace, Grace, the officer, a neighbour) and their placeholder lines from
// claude/night-0-and-the-favours-spec.md (Night 0's deal, the asks of Nights 2 and 3, a night off, his cold
// and waiting lines, Ace's line of each night, Grace's reactions, a neighbour at a window). It never changes a
// line that's already there, so his sister's rewrites stay when it runs again. Nothing in the scene changes.
//
// Since 6 Oct (claude/the-man-at-the-bins-story.md: replies and returns): Ace's replies and his answers, Night 1's
// return to the bins with the gnome (a held scene with a choice), and his "over here" lines. The deal gets its two
// choices once: only if it is still exactly the 5 Oct placeholder (the "Why?" exchange becomes the second choice's
// second reply). A deal someone has rewritten is left alone, and the report says so.
//
// Barks 2 starts a night walk lab (the Day 5 test save, never the playtest save) with the bark check, which
// photographs and checks the lines on screen (Logs/Night/barks-check-<time>/).
// ---------------------------------------------------------------------------
internal static class BarkSteps
{
    const string Tag = "[Barks] ";
    const string Menu = "Fixit Fidget/Night/";
    const string Folder = "Assets/Data/Resources";
    static string AssetPath => Folder + "/" + NightLines.ResourceName + ".asset";

    // ---- the placeholder cast and lines (Mansoor and his sister write the real ones) ----

    static readonly (string id, string name, string hex)[] Speakers =
    {
        ("lodger", "The man at the bins (placeholder: the Lodger)", "C9B284"),
        ("ace", "Ace", "4FB386"),
        ("grace", "Grace", ""),            // her own theme colour, from her profile
        ("officer", "The officer (placeholder)", "7FA3D9"),
        ("neighbour", "A neighbour", "8FA6C8"),
    };

    static readonly (string id, string speaker, string situation, string text)[] Lines =
    {
        // Night 0: the deal at the bins (the spec's §3, in its order).
        ("lodger.night0.01", "lodger", "night0.deal", "You didn't see me."),
        ("ace.night0.02", "ace", "night0.deal", "...I didn't see you."),
        ("lodger.night0.03", "lodger", "night0.deal", "You open at nine. You burn the second batch."),
        ("lodger.night0.04", "lodger", "night0.deal", "Grace likes you."),
        ("lodger.night0.05", "lodger", "night0.deal", "A café whose owner talks doesn't stay open long."),
        ("lodger.night0.06", "lodger", "night0.deal", "But I like you. So here's the thing."),
        ("lodger.night0.07", "lodger", "night0.deal", "Everything I know about this street. Learn the rest."),
        ("lodger.night0.08", "lodger", "night0.deal", "And don't get caught."),
        ("lodger.night0.09", "lodger", "night0.deal", "There's a gnome on the corner step. Bring it to me."),
        ("ace.night0.10", "ace", "night0.deal", "Why?"),
        ("lodger.night0.11", "lodger", "night0.deal", "Don't ask why. Ask how."),
        // The asks of Nights 2 and 3, and a night off.
        ("lodger.night2.01", "lodger", "night2.ask", "Grace has cups. A box of them. Get me a sleeve."),
        ("ace.night2.02", "ace", "night2.ask", "Cups? Why cups?"),
        ("lodger.night2.03", "lodger", "night2.ask", "A man needs cups."),
        ("lodger.night3.01", "lodger", "night3.ask", "Two cars parked on West Street last night."),
        ("lodger.night3.02", "lodger", "night3.ask", "Nobody parks on West Street. Cones. Both spots."),
        ("lodger.off.01", "lodger", "night.off", "Tonight, nothing. Sit. Drink."),
        // His pools: the ask again after a skip, Ace passing before it's done, and after.
        ("lodger.cold.01", "lodger", "lodger.cold", "Tonight, then."),
        ("lodger.cold.02", "lodger", "lodger.cold", "Still waiting."),
        ("lodger.cold.03", "lodger", "lodger.cold", "I asked nicely once."),
        ("lodger.waiting.01", "lodger", "lodger.waiting", "Not yet."),
        ("lodger.waiting.02", "lodger", "lodger.waiting", "You're going the wrong way."),
        ("lodger.waiting.03", "lodger", "lodger.waiting", "I'm not going anywhere."),
        ("lodger.done.01", "lodger", "lodger.done", "Good."),
        ("lodger.done.02", "lodger", "lodger.done", "Not bad. Not bad at all."),
        ("lodger.done.03", "lodger", "lodger.done", "See? Easy."),
        // Ace's line of the night (the spec's §7), one a night.
        ("ace.line.night0", "ace", "ace.night", "I own a café and a man lives in my bins."),
        ("ace.line.night1", "ace", "ace.night", "It's a gnome. It's just a gnome."),
        ("ace.line.night2", "ace", "ace.night", "If she wakes up I'm a burglar. If not, a guest."),
        ("ace.line.night3", "ace", "ace.night", "Stupidest thing I've ever done. Quite well done."),
        ("ace.line.day5", "ace", "ace.night", "I'm getting good at this. That's the worrying part."),
        // Night 0's replies (6 Oct): Ace answers twice in the deal; each reply has a line said back.
        ("ace.night0.02b", "ace", "night0.deal", "You were in my bin."),
        ("lodger.night0.01a", "lodger", "night0.deal", "Good. Quick."),
        ("lodger.night0.01b", "lodger", "night0.deal", "And now I'm not. Keep up."),
        ("ace.night0.10a", "ace", "night0.deal", "Fine."),
        ("lodger.night0.11a", "lodger", "night0.deal", "Good. Don't drop him."),
        // Night 1's return: Ace brings the gnome back to the bins.
        ("lodger.night1.r01", "lodger", "night1.return", "There he is."),
        ("ace.night1.r02a", "ace", "night1.return", "Here. Happy?"),
        ("ace.night1.r02b", "ace", "night1.return", "What's he even for?"),
        ("lodger.night1.r03a", "lodger", "night1.return", "Very."),
        ("lodger.night1.r03b", "lodger", "night1.return", "Watch."),
        ("lodger.night1.r04", "lodger", "night1.return", "It was watching me. Now it watches for me."),
        ("lodger.night1.r05", "lodger", "night1.return", "You kept your nerve. Keep it tomorrow, when she asks."),
        ("lodger.night1.r06", "lodger", "night1.return", "And a page for your notebook."),
        ("lodger.night1.r07", "lodger", "night1.return", "Tomorrow, her kitchen."),
        // His pool while Ace has his thing in hand.
        ("lodger.beckon.01", "lodger", "lodger.beckon", "Over here."),
        ("lodger.beckon.02", "lodger", "lodger.beckon", "Bring him here."),
        ("lodger.beckon.03", "lodger", "lodger.beckon", "That's the one."),
        // Grace at home (chunk C): noticing, and catching.
        ("grace.notice.01", "grace", "grace.notice", "Hm?"),
        ("grace.notice.02", "grace", "grace.notice", "Is someone there?"),
        ("grace.notice.03", "grace", "grace.notice", "Hello?"),
        ("grace.caught.01", "grace", "grace.caught", "Ace?! What on earth—"),
        // A neighbour at a window.
        ("neighbour.window.01", "neighbour", "neighbour.window", "Who's out there at this hour?"),
        ("neighbour.window.02", "neighbour", "neighbour.window", "Some of us sleep, you know."),
        ("neighbour.window.03", "neighbour", "neighbour.window", "Go home!"),
    };

    // The deal as Barks 1 first made it (5 Oct), and as it is with Ace's replies (6 Oct): his "...I didn't see you."
    // and "Why?" become choices, each answered.
    static readonly string[] DealOfFifthOctober =
    {
        "lodger.night0.01", "ace.night0.02", "lodger.night0.03", "lodger.night0.04", "lodger.night0.05",
        "lodger.night0.06", "lodger.night0.07", "lodger.night0.08", "lodger.night0.09", "ace.night0.10", "lodger.night0.11",
    };
    static readonly string[] DealWithReplies =
    {
        "lodger.night0.01", "lodger.night0.03", "lodger.night0.04", "lodger.night0.05",
        "lodger.night0.06", "lodger.night0.07", "lodger.night0.08", "lodger.night0.09",
    };

    static readonly (string id, bool hold, string[] lines)[] Scenes =
    {
        ("night0.deal", true, DealWithReplies),
        ("night1.return", true, new[] { "lodger.night1.r01", "lodger.night1.r04", "lodger.night1.r05", "lodger.night1.r06", "lodger.night1.r07" }),
        ("night2.ask", false, new[] { "lodger.night2.01", "ace.night2.02", "lodger.night2.03" }),
        ("night3.ask", false, new[] { "lodger.night3.01", "lodger.night3.02" }),
        ("night.off", false, new[] { "lodger.off.01" }),
    };

    // Each scene's choices: after which line, then each reply's line, the line said back and its warmth.
    static readonly (string scene, string after, string a, string aBack, int aWarm, string b, string bBack, int bWarm)[] Choices =
    {
        ("night0.deal", "lodger.night0.01", "ace.night0.02", "lodger.night0.01a", 1, "ace.night0.02b", "lodger.night0.01b", -1),
        ("night0.deal", "lodger.night0.09", "ace.night0.10a", "lodger.night0.11a", 1, "ace.night0.10", "lodger.night0.11", 0),
        ("night1.return", "lodger.night1.r01", "ace.night1.r02a", "lodger.night1.r03a", 1, "ace.night1.r02b", "lodger.night1.r03b", 0),
    };

    // ================================================================== Barks 1

    [MenuItem(Menu + "Barks 1 - Make or update the night lines (placeholders)")]
    static void MakeLines()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Resources");
            var asset = AssetDatabase.LoadAssetAtPath<NightLines>(AssetPath);
            bool made = asset == null;
            if (made)
            {
                asset = ScriptableObject.CreateInstance<NightLines>();
                asset.name = NightLines.ResourceName;
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            Undo.RecordObject(asset, "Night lines");
            int speakers = 0, lines = 0, scenes = 0;
            var speakerList = asset.speakers.ToList();
            foreach (var (id, name, hex) in Speakers)
            {
                if (speakerList.Any(s => s != null && s.id == id)) continue;
                speakerList.Add(new NightLines.Speaker { id = id, name = name, colour = ColourFor(id, hex), sound = "" });
                speakers++;
            }
            var lineList = asset.lines.ToList();
            foreach (var (id, speaker, situation, text) in Lines)
            {
                if (lineList.Any(l => l != null && l.id == id)) continue;
                lineList.Add(new NightLines.Line { id = id, speaker = speaker, situation = situation, text = text });
                lines++;
            }
            var sceneList = asset.scenes.ToList();
            foreach (var (id, hold, ids) in Scenes)
            {
                if (sceneList.Any(s => s != null && s.id == id)) continue;
                sceneList.Add(new NightLines.Scene { id = id, holdAce = hold, lines = ids.ToArray(), choices = ChoicesOf(id) });
                scenes++;
            }
            // The deal of 5 Oct, untouched since: its lines as they are with Ace's replies, and the replies.
            string dealNote = "";
            NightLines.Scene deal = sceneList.FirstOrDefault(x => x != null && x.id == "night0.deal");
            if (deal != null && (deal.choices == null || deal.choices.Length == 0))
            {
                if (deal.lines != null && deal.lines.SequenceEqual(DealOfFifthOctober))
                {
                    deal.lines = DealWithReplies.ToArray();
                    deal.choices = ChoicesOf("night0.deal");
                    dealNote = " The deal now has Ace's two replies.";
                }
                else dealNote = " The deal has been rewritten since 5 Oct, so its replies were not added: add them in its Choices.";
            }
            asset.speakers = speakerList.ToArray();
            asset.lines = lineList.ToArray();
            asset.scenes = sceneList.ToArray();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            Debug.Log(Tag + $"{(made ? "Made" : "Updated")} {AssetPath}: {speakers} speaker(s), {lines} line(s) and {scenes} scene(s) added " +
                      $"({asset.speakers.Length} speakers, {asset.lines.Length} lines, {asset.scenes.Length} scenes in all; no line already there was changed).{dealNote} " +
                      "All placeholder copy. Next: Fixit Fidget > Checks > Bark rules, then Barks 2.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "The night lines FAILED: " + e.Message + "\n" + e);
        }
    }

    static NightLines.Choice[] ChoicesOf(string scene) => Choices.Where(c => c.scene == scene).Select(c => new NightLines.Choice
    {
        after = c.after,
        first = new NightLines.Reply { line = c.a, answer = c.aBack, warmth = c.aWarm },
        second = new NightLines.Reply { line = c.b, answer = c.bBack, warmth = c.bWarm },
    }).ToArray();

    static Color ColourFor(string id, string hex)
    {
        if (id == "grace")
        {
            // Her own theme colour, the one her name wears in conversations.
            foreach (string guid in AssetDatabase.FindAssets("t:CustomerProfile"))
            {
                var profile = AssetDatabase.LoadAssetAtPath<CustomerProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile != null && profile.characterName == "Grace") return new Color(profile.themeColor.r, profile.themeColor.g, profile.themeColor.b, 1f);
            }
            hex = "D98C5F";
        }
        return ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.white;
    }

    // ================================================================== Barks 2

    [MenuItem(Menu + "Barks 2 - Check barks (lab, Play Mode)")]
    static void CheckBarks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<NightLines>(AssetPath) == null)
        {
            Debug.LogError(Tag + "There are no night lines yet: run Barks 1 first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData { day = 5, money = 600, cups = 40, beans = 40, dayCompleted = false });
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(NightWalk.PendingKey, 1);
        PlayerPrefs.SetInt(BarkCheck.LabKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + $"Bark check: a night walk lab session (Day 5 test save {path}; your playtest save is not used). " +
                  "Leave the mouse and keyboard alone for about a minute; the report goes to Logs/Night/barks-check-<time>/.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu + "Barks 1 - Make or update the night lines (placeholders)", true)]
    [MenuItem(Menu + "Barks 2 - Check barks (lab, Play Mode)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A check asked for that never became a Play session must not run in the next one.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(BarkCheck.LabKey))
        {
            PlayerPrefs.DeleteKey(BarkCheck.LabKey);
            PlayerPrefs.Save();
        }
    }
}
#endif
