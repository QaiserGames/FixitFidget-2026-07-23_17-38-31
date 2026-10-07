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
// Session 3 (claude/session-3-favours-stalling-officer.md): Night 2's ask (held, with Ace's reply; and its warm
// version), the cups' return, a night with nothing yet, the lines he says again after a skip, his verdicts on the day,
// his lines in the café, and the cop line at the end of the deal. Three placeholders of 5 Oct and 6 Oct are upgraded
// once, only if they are still exactly as they were made (anything rewritten is left alone and the report says so):
// the deal gets the cop line; Night 2's ask becomes held, with a reply; the night off holds Ace. One line's words are
// changed the same way, only if untouched: "Bring him here." (the gnome) becomes "Bring it here." (any favour).
//
// Playtest 3 (6 Oct 2026, claude/playtest-3-notes-and-plan.md §5.1): every placeholder to the word budget (WordBudget),
// the deal to eight lines, the gnome's return ending on the hold, the cups' return without the page and the cars.
// Rewordings (Retexts) and reshapings (Rescenes) happen once, only to a line or scene still exactly as Barks 1 made it;
// anything rewritten since is left alone and the report says so.
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
        // Night 0: the deal at the bins (the spec's §3, in its order; eight lines since playtest 3: 04 and 08 folded into 05 and 13).
        ("lodger.night0.01", "lodger", "night0.deal", "You didn't see me."),
        ("ace.night0.02", "ace", "night0.deal", "...I didn't see you."),
        ("lodger.night0.03", "lodger", "night0.deal", "You open at nine. You burn the second batch."),
        ("lodger.night0.05", "lodger", "night0.deal", "Grace likes you. Café owners who talk don't last."),
        ("lodger.night0.06", "lodger", "night0.deal", "But I like you. So here's the thing."),
        ("lodger.night0.07", "lodger", "night0.deal", "Everything I know about this street. Learn the rest."),
        ("lodger.night0.09", "lodger", "night0.deal", "A gnome on the corner step. Bring it here."),
        ("ace.night0.10", "ace", "night0.deal", "Why?"),
        ("lodger.night0.11", "lodger", "night0.deal", "Don't ask why. Ask how."),
        // The asks of Nights 2 and 3, and a night off.
        ("lodger.night2.01", "lodger", "night2.ask", "Grace has a box of cups. Get me a sleeve."),
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
        ("ace.line.night0", "ace", "ace.night", "I own a café. A man lives in my bins."),
        ("ace.line.night1", "ace", "ace.night", "It's a gnome. It's just a gnome."),
        ("ace.line.night2", "ace", "ace.night", "If she wakes, I'm a burglar. Otherwise, a guest."),
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
        ("lodger.night1.r05", "lodger", "night1.return", "You kept your nerve. Keep it when she asks."),
        ("lodger.night1.r06", "lodger", "night1.return", "A page for you. Grace's Thursdays."),
        ("lodger.night1.r07", "lodger", "night1.return", "We've both got something on each other now. Mine's bigger."),
        // His pool while Ace has his thing in hand.
        ("lodger.beckon.01", "lodger", "lodger.beckon", "Over here."),
        ("lodger.beckon.02", "lodger", "lodger.beckon", "Bring it here."),
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
        // Session 3: the cop line at the end of the deal (the officer drinks Ace's coffee from Day 1).
        ("lodger.night0.12", "lodger", "night0.deal", "That cop who drinks your coffee? He's looking for me."),
        ("lodger.night0.13", "lodger", "night0.deal", "Keep him happy. And don't get caught."),
        // Night 2's ask: Ace answers; warm, he says why before Ace asks.
        ("ace.night2.02a", "ace", "night2.ask", "One sleeve. Fine."),
        ("lodger.night2.03a", "lodger", "night2.ask", "Good. Don't wake her."),
        ("lodger.night2.04", "lodger", "night2.ask", "Her kitchen. A box on the worktop."),
        ("lodger.night2.w02", "lodger", "night2.ask", "Before you ask: old times. You'll see."),
        ("ace.night2.02w", "ace", "night2.ask", "Old times?"),
        ("lodger.night2.03w", "lodger", "night2.ask", "You'll see. Bring them back."),
        // Night 2's return: four cups on the crate.
        ("lodger.night2.r01", "lodger", "night2.return", "You found them."),
        ("ace.night2.r02a", "ace", "night2.return", "One sleeve, as asked."),
        ("ace.night2.r02b", "ace", "night2.return", "So what are they for?"),
        ("lodger.night2.r03a", "lodger", "night2.return", "As asked. I like that."),
        ("lodger.night2.r03b", "lodger", "night2.return", "Watch."),
        ("lodger.night2.r04", "lodger", "night2.return", "Four cups. There used to be four of us."),
        ("lodger.night2.r05", "lodger", "night2.return", "You walk like a cop. I can fix that."),
        ("lodger.night2.r06", "lodger", "night2.return", "And a page. Her photos. Look who isn't in them."),
        ("lodger.night2.r07", "lodger", "night2.return", "Two cars on West Street. I want them gone."),
        // A night with nothing to ask yet (the next favour isn't in the game).
        ("lodger.wait.01", "lodger", "night.wait", "Nothing tonight. I'm still thinking."),
        // What he wants, said again after his cold line (the ask again, after a skip).
        ("lodger.remind.gnome", "lodger", "lodger.remind", "The gnome. Grace's step."),
        ("lodger.remind.cups", "lodger", "lodger.remind", "Grace's cups. One sleeve."),
        ("lodger.remind.cones", "lodger", "lodger.remind", "The cones. West Street."),
        // His verdict on the day just ended, before tonight's ask.
        ("lodger.verdict.held.01", "lodger", "lodger.verdict.held", "She asked. You held. Good."),
        ("lodger.verdict.held.02", "lodger", "lodger.verdict.held", "Straight face this morning. I saw."),
        ("lodger.verdict.cracked.01", "lodger", "lodger.verdict.cracked", "You smiled at her. She'll remember."),
        ("lodger.verdict.cracked.02", "lodger", "lodger.verdict.cracked", "Your face this morning. Work on it."),
        ("lodger.verdict.quiet.01", "lodger", "lodger.verdict.quiet", "The cop asked. You said nothing. Good."),
        ("lodger.verdict.flinched.01", "lodger", "lodger.verdict.flinched", "Flinched at the cop. He'll be back."),
        // In the café, the morning after a skip: one line when Ace passes, by how warm he is.
        ("lodger.visit.01", "lodger", "lodger.visit", "Nice place. Shame about last night."),
        ("lodger.visit.02", "lodger", "lodger.visit", "Good coffee. I'll wait."),
        ("lodger.visit.warm.01", "lodger", "lodger.visit.warm", "Nice place. Don't take too long."),
        ("lodger.visit.cold.01", "lodger", "lodger.visit.cold", "Nice place. Shame if people asked questions."),
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

    // The deal with the cop line at its end (session 3).
    static readonly string[] DealWithTheCop =
    {
        "lodger.night0.01", "lodger.night0.03", "lodger.night0.04", "lodger.night0.05",
        "lodger.night0.06", "lodger.night0.07", "lodger.night0.08", "lodger.night0.09", "lodger.night0.12", "lodger.night0.13",
    };
    // The deal at eight lines (playtest 3): "Grace likes you" joins the talkers' line, "don't get caught" joins the cop's.
    static readonly string[] DealEight =
    {
        "lodger.night0.01", "lodger.night0.03", "lodger.night0.05", "lodger.night0.06",
        "lodger.night0.07", "lodger.night0.09", "lodger.night0.12", "lodger.night0.13",
    };
    // The cups' return as session 3 made it, and without the page (her photos wait for chunk D) and the cars (the cones).
    static readonly string[] CupsReturnOfSessionThree = { "lodger.night2.r01", "lodger.night2.r04", "lodger.night2.r05", "lodger.night2.r06", "lodger.night2.r07" };
    static readonly string[] CupsReturn = { "lodger.night2.r01", "lodger.night2.r04", "lodger.night2.r05" };
    // Night 2's ask as Barks 1 first made it (5 Oct: not held, Ace's "Why cups?" a line of the scene), and as it is now.
    static readonly string[] NightTwoAskOfFifthOctober = { "lodger.night2.01", "ace.night2.02", "lodger.night2.03" };
    static readonly string[] NightTwoAsk = { "lodger.night2.01", "lodger.night2.04" };
    static readonly string[] NightOff = { "lodger.off.01" };

    static readonly (string id, bool hold, string[] lines)[] Scenes =
    {
        ("night0.deal", true, DealEight),
        ("night1.return", true, new[] { "lodger.night1.r01", "lodger.night1.r04", "lodger.night1.r05", "lodger.night1.r06", "lodger.night1.r07" }),
        ("night2.ask", true, NightTwoAsk),
        ("night2.ask.warm", true, new[] { "lodger.night2.01", "lodger.night2.w02", "lodger.night2.04" }),
        ("night2.return", true, CupsReturn),
        ("night3.ask", false, new[] { "lodger.night3.01", "lodger.night3.02" }),
        ("night.off", true, NightOff),
        ("night.wait", true, new[] { "lodger.wait.01" }),
    };

    // A placeholder's words changed once, only if they are still exactly as they were made.
    static readonly (string id, string was, string now)[] Retexts =
    {
        ("lodger.beckon.02", "Bring him here.", "Bring it here."),
        // Playtest 3: to the word budget; the hold; Soft feet.
        ("lodger.night0.05", "A café whose owner talks doesn't stay open long.", "Grace likes you. Café owners who talk don't last."),
        ("lodger.night0.09", "There's a gnome on the corner step. Bring it to me.", "A gnome on the corner step. Bring it here."),
        ("lodger.night0.13", "Keep him happy.", "Keep him happy. And don't get caught."),
        ("lodger.night1.r05", "You kept your nerve. Keep it tomorrow, when she asks.", "You kept your nerve. Keep it when she asks."),
        ("lodger.night1.r06", "And a page for your notebook.", "A page for you. Grace's Thursdays."),
        ("lodger.night1.r07", "Tomorrow, her kitchen.", "We've both got something on each other now. Mine's bigger."),
        ("lodger.night2.01", "Grace has cups. A box of them. Get me a sleeve.", "Grace has a box of cups. Get me a sleeve."),
        ("lodger.night2.r04", "Four cups. Old habit. There used to be four of us.", "Four cups. There used to be four of us."),
        ("lodger.night2.r05", "Doors. Every house has a key. People tell you where.", "You walk like a cop. I can fix that."),
        ("ace.line.night0", "I own a café and a man lives in my bins.", "I own a café. A man lives in my bins."),
        ("ace.line.night2", "If she wakes up I'm a burglar. If not, a guest.", "If she wakes, I'm a burglar. Otherwise, a guest."),
        ("lodger.verdict.cracked.01", "You smiled at her this morning. She'll remember.", "You smiled at her. She'll remember."),
        ("lodger.verdict.quiet.01", "The cop asked about me. You said nothing. Good.", "The cop asked. You said nothing. Good."),
        ("lodger.verdict.flinched.01", "The cop asked. You flinched. He'll be back.", "Flinched at the cop. He'll be back."),
        ("lodger.visit.warm.01", "Nice place. Take your time. Not too much.", "Nice place. Don't take too long."),
        ("lodger.visit.cold.01", "Nice place. Be a shame if people asked questions.", "Nice place. Shame if people asked questions."),
    };

    // A scene's lines changed once, only if they are still exactly as they were made (the choices stay).
    static readonly (string id, string[] was, string[] now, string note)[] Rescenes =
    {
        ("night0.deal", DealWithTheCop, DealEight, "The deal is eight lines now."),
        ("night2.return", CupsReturnOfSessionThree, CupsReturn, "The cups' return is three lines now (no page, no cars)."),
    };

    // Each scene's choices: after which line, then each reply's line, the line said back and its warmth.
    static readonly (string scene, string after, string a, string aBack, int aWarm, string b, string bBack, int bWarm)[] Choices =
    {
        ("night0.deal", "lodger.night0.01", "ace.night0.02", "lodger.night0.01a", 1, "ace.night0.02b", "lodger.night0.01b", -1),
        ("night0.deal", "lodger.night0.09", "ace.night0.10a", "lodger.night0.11a", 1, "ace.night0.10", "lodger.night0.11", 0),
        ("night1.return", "lodger.night1.r01", "ace.night1.r02a", "lodger.night1.r03a", 1, "ace.night1.r02b", "lodger.night1.r03b", 0),
        ("night2.ask", "lodger.night2.01", "ace.night2.02a", "lodger.night2.03a", 1, "ace.night2.02", "lodger.night2.03", 0),
        ("night2.ask.warm", "lodger.night2.w02", "ace.night2.02a", "lodger.night2.03a", 1, "ace.night2.02w", "lodger.night2.03w", 0),
        ("night2.return", "lodger.night2.r01", "ace.night2.r02a", "lodger.night2.r03a", 1, "ace.night2.r02b", "lodger.night2.r03b", 0),
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
            // Session 3: the cop line at the deal's end, once, only if the deal is as Barks 1 made it on 6 Oct.
            if (deal != null && deal.lines != null && !deal.lines.Contains("lodger.night0.12"))
            {
                if (deal.lines.SequenceEqual(DealWithReplies))
                {
                    deal.lines = DealWithTheCop.ToArray();
                    dealNote += " The deal now ends with the cop line.";
                }
                else dealNote += " The deal has been rewritten, so the cop line (lodger.night0.12, .13) was not added: add it to its Lines.";
            }
            // Night 2's ask of 5 Oct, untouched: held, with Ace's reply. The night off of 5 Oct, untouched: held.
            NightLines.Scene nightTwo = sceneList.FirstOrDefault(x => x != null && x.id == "night2.ask");
            if (nightTwo != null && !nightTwo.holdAce && (nightTwo.choices == null || nightTwo.choices.Length == 0))
            {
                if (nightTwo.lines != null && nightTwo.lines.SequenceEqual(NightTwoAskOfFifthOctober))
                {
                    nightTwo.lines = NightTwoAsk.ToArray();
                    nightTwo.holdAce = true;
                    nightTwo.choices = ChoicesOf("night2.ask");
                    dealNote += " Night 2's ask now holds Ace, with a reply.";
                }
                else dealNote += " Night 2's ask has been rewritten, so it was left alone: it should hold Ace and have a choice.";
            }
            NightLines.Scene off = sceneList.FirstOrDefault(x => x != null && x.id == "night.off");
            if (off != null && !off.holdAce && off.lines != null && off.lines.SequenceEqual(NightOff)) off.holdAce = true;
            int retexts = 0;
            var kept = new List<string>();
            foreach (var (id, was, now) in Retexts)
            {
                NightLines.Line line = lineList.FirstOrDefault(l => l != null && l.id == id);
                if (line == null || line.text == now) continue;
                if (line.text == was) { line.text = now; retexts++; }
                else kept.Add(id);
            }
            if (retexts > 0) dealNote += $" {retexts} placeholder line(s) reworded.";
            if (kept.Count > 0) dealNote += $" Rewritten since, so left alone: {string.Join(", ", kept)}.";
            foreach (var (id, was, now, note) in Rescenes)
            {
                NightLines.Scene scene = sceneList.FirstOrDefault(x => x != null && x.id == id);
                if (scene == null || scene.lines == null || scene.lines.SequenceEqual(now)) continue;
                if (scene.lines.SequenceEqual(was)) { scene.lines = now.ToArray(); dealNote += " " + note; }
                else dealNote += $" Scene '{id}' has been reshaped since, so it was left alone ({note.TrimEnd('.')} would have been applied).";
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
