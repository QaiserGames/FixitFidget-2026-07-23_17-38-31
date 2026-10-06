using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

// Rule checks for barks (claude/foundation-pass-build-plan.md §5): how long a line stays up, the ambient
// throttle, and the pools. Pure: no scene, no save, no assets. Also compiled by Tests/BarkRules. The
// in-Editor menu (Fixit Fidget > Checks > Bark rules) also checks the Night lines asset: ids, speakers,
// scenes, blank lines, and the 60-character writing rule (a warning, not a failure). Since 6 Oct, Ace's replies
// too: each choice in a held scene, after one of its lines, both replies Ace's lines (32 characters for a chip:
// a warning), the lines said back there, the warmth small; and the scenes the man at the bins plays (LodgerStory).
// Session 3: every favour's ask (and its warm one), its line to say again, its return and the lines things happen on;
// a night off and a night with nothing yet; his cold, verdict and visit pools; Ace's line for each favour.
public static class BarkRuleChecks
{
#if UNITY_EDITOR
    [MenuItem("Fixit Fidget/Checks/Bark rules")]
    public static void Run()
    {
        try
        {
            int count = RunAll();
            var warnings = new List<string>();
            int asset = CheckNightLines(warnings);
            string tail = warnings.Count == 0 ? "" : "\n" + warnings.Count + " warning(s):\n  " + string.Join("\n  ", warnings);
            Debug.Log("[Bark rules] PASS: " + count + " rule assertions, " + asset + " Night lines assertions. No scene, save or asset changes." + tail);
        }
        catch (Exception e)
        {
            Debug.LogError("[Bark rules] FAILED: " + e.Message + "\n" + e);
        }
    }

    // The Night lines asset (Resources/Night lines): every id unique, every line's speaker known, every scene's
    // lines there, nothing blank; lines over the writing rule are listed as warnings.
    static int CheckNightLines(List<string> warnings)
    {
        int n = 0;
        void Check(bool ok, string why) { n++; if (!ok) throw new InvalidOperationException(why); }
        NightLines lines = Resources.Load<NightLines>(NightLines.ResourceName);
        Check(lines != null, "The Night lines asset is in Resources (Fixit Fidget > Night > Barks 1 makes it).");
        var speakers = new HashSet<string>(StringComparer.Ordinal);
        foreach (NightLines.Speaker s in lines.speakers)
        {
            Check(s != null && !string.IsNullOrWhiteSpace(s.id), "Every speaker has an id.");
            Check(speakers.Add(s.id), $"Speaker '{s.id}' is listed once.");
            Check(s.colour.a > .5f, $"Speaker '{s.id}' has a colour you can see (alpha over 0.5).");
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (NightLines.Line l in lines.lines)
        {
            Check(l != null && !string.IsNullOrWhiteSpace(l.id), "Every line has an id.");
            Check(ids.Add(l.id), $"Line '{l.id}' is listed once.");
            Check(speakers.Contains(l.speaker), $"Line '{l.id}': its speaker '{l.speaker}' is a known speaker.");
            Check(!string.IsNullOrWhiteSpace(l.text), $"Line '{l.id}' isn't blank.");
            Check(!string.IsNullOrWhiteSpace(l.situation), $"Line '{l.id}' has a situation.");
            if (l.text.Length > BarkRules.LengthRule)
                warnings.Add($"'{l.id}' is {l.text.Length} characters (the rule is {BarkRules.LengthRule}): \"{l.text}\"");
        }
        var scenes = new HashSet<string>(StringComparer.Ordinal);
        foreach (NightLines.Scene s in lines.scenes)
        {
            Check(s != null && !string.IsNullOrWhiteSpace(s.id), "Every scene has an id.");
            Check(scenes.Add(s.id), $"Scene '{s.id}' is listed once.");
            Check(s.lines != null && s.lines.Length > 0, $"Scene '{s.id}' has lines.");
            foreach (string id in s.lines) Check(ids.Contains(id), $"Scene '{s.id}': its line '{id}' is in the asset.");
            foreach (NightLines.Choice c in s.choices ?? Array.Empty<NightLines.Choice>())
            {
                Check(c != null && Array.IndexOf(s.lines, c.after) >= 0, $"Scene '{s.id}': a choice comes after one of its own lines ('{c?.after}').");
                Check(s.holdAce, $"Scene '{s.id}': a scene with a choice holds Ace (only a held scene waits for the reply).");
                foreach (NightLines.Reply r in new[] { c.first, c.second })
                {
                    NightLines.Line said = lines.FindLine(r?.line);
                    Check(said != null && said.speaker == "ace", $"Scene '{s.id}', after '{c.after}': each reply is one of Ace's lines ('{r?.line}').");
                    Check(string.IsNullOrEmpty(r.answer) || ids.Contains(r.answer), $"Scene '{s.id}', after '{c.after}': the line said back ('{r.answer}') is in the asset.");
                    Check(Math.Abs(r.warmth) <= 2, $"Scene '{s.id}', after '{c.after}': a reply moves his warmth by 2 at most.");
                    if (said.text.Length > NightLines.ReplyRule)
                        warnings.Add($"Reply '{said.id}' is {said.text.Length} characters (a chip holds {NightLines.ReplyRule}): \"{said.text}\"");
                }
            }
        }
        // The man at the bins plays these, and does things on these lines (LodgerStory).
        Check(scenes.Contains(LodgerStory.DealScene) && scenes.Contains(LodgerStory.ReturnScene), "The deal and the return are scenes in the asset (Barks 1 adds them).");
        Check(Array.IndexOf(lines.FindScene(LodgerStory.DealScene).lines, LodgerStory.HandOverLine) >= 0, "The deal says the line on which the notebook changes hands.");
        foreach (string id in new[] { LodgerStory.TakesItLine, LodgerStory.TurnsItLine, LodgerStory.LessonLine, LodgerStory.PageLine })
            Check(Array.IndexOf(lines.FindScene(LodgerStory.ReturnScene).lines, id) >= 0, $"The return says '{id}' (what he does happens on it).");
        foreach (string pool in new[] { LodgerStory.Waiting, LodgerStory.Beckon, LodgerStory.Done, LodgerStory.Cold,
                     LodgerStory.VerdictHeld, LodgerStory.VerdictCracked, LodgerStory.VerdictQuiet, LodgerStory.VerdictFlinched,
                     LodgerStory.Visit, LodgerStory.VisitWarm, LodgerStory.VisitCold })
            Check(lines.Pool(LodgerStory.SpeakerId, pool).Count > 0, $"He has lines for '{pool}'.");
        Check(ids.Contains(LodgerStory.AceNightZero) && ids.Contains(LodgerStory.AceNightOne), "Ace's lines of Nights 0 and 1 are in the asset.");
        foreach (string scene in new[] { LodgerStory.OffScene, LodgerStory.WaitScene })
            Check(scenes.Contains(scene) && lines.FindScene(scene).holdAce, $"'{scene}' is a scene that holds Ace (it plays at the bins).");
        // Each favour he can ask for in the game: its ask, the line said again, the return.
        foreach (LodgerStory.Favour f in LodgerStory.Favours)
        {
            if (NightThings.Find(f.id) == null) continue;   // not in the game yet (the cones)
            Check(ids.Contains(f.remind), $"Favour '{f.id}': the line he says again after a skip ('{f.remind}') is in the asset.");
            Check(ids.Contains(f.aceLine), $"Favour '{f.id}': Ace's line on taking it ('{f.aceLine}') is in the asset.");
            foreach (string ask in new[] { f.ask, f.askWarm })
            {
                if (string.IsNullOrEmpty(ask)) continue;
                NightLines.Scene asked = lines.FindScene(ask);
                Check(asked != null && asked.holdAce && asked.choices != null && asked.choices.Length > 0,
                    $"Favour '{f.id}': its ask '{ask}' is a held scene where Ace answers.");
                Check(string.IsNullOrEmpty(f.firstLine) || Array.IndexOf(asked.lines, f.firstLine) >= 0,
                    $"Favour '{f.id}': its ask '{ask}' says '{f.firstLine}' (Ace hears of the thing on it).");
            }
            NightLines.Scene back = lines.FindScene(f.returnScene);
            Check(back != null && back.holdAce && back.choices != null && back.choices.Length > 0, $"Favour '{f.id}': its return '{f.returnScene}' is a held scene where Ace answers.");
            foreach (string id in new[] { f.takes, f.sets, f.lesson, f.page })
                Check(Array.IndexOf(back.lines, id) >= 0, $"Favour '{f.id}': its return says '{id}' (what he does happens on it).");
        }
        return n;
    }
#endif

    public static int RunAll()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new InvalidOperationException(why); }

        // ---------- how long a line stays up (the conversation's reading time) ----------
        var rules = new BarkRules(7);
        Check(Math.Abs(rules.ReadingSeconds("Hm?") - 1.4f) < 1e-4f, "A very short line still stays up 1.4 s.");
        Check(Math.Abs(rules.ReadingSeconds(new string('x', 200)) - 4.5f) < 1e-4f, "A long line stays up at most 4.5 s.");
        const string Gnome = "There's a gnome on the corner step. Bring it to me.";   // 51 characters
        float mid = rules.ReadingSeconds(Gnome);
        Check(Math.Abs(mid - (.6f + Gnome.Length / 18f)) < 1e-3f && Gnome.Length == 51, "In between, 0.6 s plus 1/18 s a character.");
        Check(Math.Abs(rules.ReadingSeconds(null) - 1.4f) < 1e-4f && Math.Abs(rules.ReadingSeconds("") - 1.4f) < 1e-4f, "No text is the shortest time, not an error.");

        // ---------- the ambient throttle ----------
        rules = new BarkRules(7) { speakerCooldown = 20f, globalGap = 4f };
        Check(rules.MayAmbient("lodger", 0f, false), "Nobody has spoken: the first ambient line may start.");
        Check(!rules.MayAmbient("lodger", 0f, true), "Never while a scene plays.");
        Check(!rules.MayAmbient("", 0f, false) && !rules.MayAmbient(null, 0f, false), "A line needs a speaker.");
        rules.SaidAmbient("lodger", 0f);
        Check(!rules.MayAmbient("neighbour", 3.9f, false), "Anyone waits the global gap (4 s) after an ambient line.");
        Check(rules.MayAmbient("neighbour", 4f, false), "Someone else may speak once the gap is over.");
        Check(!rules.MayAmbient("lodger", 19.9f, false), "The same speaker waits their cooldown (20 s).");
        Check(rules.MayAmbient("lodger", 20f, false), "Then they may speak again.");
        Check(Math.Abs(rules.Wait("lodger", 5f) - 15f) < 1e-4f, "Wait says how long a speaker has left (15 s at 5 s).");
        Check(Math.Abs(rules.Wait("neighbour", 1f) - 3f) < 1e-4f, "Wait says the global gap for anyone else (3 s at 1 s).");
        Check(rules.Wait("neighbour", 10f) == 0f, "Nothing to wait: 0.");
        rules.SaidAmbient("neighbour", 10f);
        Check(!rules.MayAmbient("lodger", 12f, false) && rules.MayAmbient("lodger", 20f, false), "Each speaker's cooldown and the global gap apply together.");
        rules.Reset();
        Check(rules.MayAmbient("neighbour", 10.5f, false), "A reset forgets every gap.");

        // ---------- the pools: every line once before any repeats, never twice in a row ----------
        rules = new BarkRules(11);
        Check(rules.Next("lodger/cold", 0) == -1 && rules.Next(null, 3) == -1, "An empty pool has no next line.");
        Check(rules.Next("lodger/one", 1) == 0 && rules.Next("lodger/one", 1) == 0, "A pool of one says its line every time.");
        int previous = -1;
        for (int round = 0; round < 40; round++)
        {
            var seen = new HashSet<int>();
            for (int k = 0; k < 4; k++)
            {
                int i = rules.Next("lodger/waiting", 4);
                Check(i >= 0 && i < 4, "A pool's next line is one of its lines.");
                Check(seen.Add(i), "Within a round, no line repeats.");
                Check(i != previous, "Never the same line twice in a row, across rounds too.");
                previous = i;
            }
        }
        // A pool whose size changed starts a new round with the new size.
        var after = new HashSet<int>();
        for (int k = 0; k < 6; k++) after.Add(rules.Next("lodger/waiting", 6));
        Check(after.Count == 6, "A pool that grew starts a new round with every line in it.");
        // Two pools keep their own rounds.
        var a = new HashSet<int>();
        var b = new HashSet<int>();
        for (int k = 0; k < 3; k++) { a.Add(rules.Next("grace/notice", 3)); b.Add(rules.Next("neighbour/window", 3)); }
        Check(a.Count == 3 && b.Count == 3, "Each pool keeps its own round.");

        // ---------- the writing rule ----------
        Check(BarkRules.LengthRule == 60, "The writing rule is 60 characters a line (two lines on screen).");
        return count;
    }
}
