using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

// Rule checks for Grace at home (break-ins chunk C, 6 Oct 2026; claude/chunk-c-grace-at-home-plan.md): the days of the
// week, her night by the clock (a usual night and her Thursday), how fast her mark fills and drains, what a sound does
// to it, and the ways she walks in her house. Pure: no scene, no save file, no assets. Also compiled by Tests/GraceRules.
public static class GraceRuleChecks
{
#if UNITY_EDITOR
    [MenuItem("Fixit Fidget/Checks/Grace at home rules")]
    public static void Run()
    {
        int count = RunAll();
        Debug.Log("[Grace at home rules] PASS: " + count + " assertions. No scene, save or asset changes.");
    }
#endif

    public static int RunAll()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new InvalidOperationException(why); }
        bool Near(float a, float b, float within = 1e-3f) => Math.Abs(a - b) <= within;

        // ---------- the days of the week ----------
        Check(Weekdays.Of(1) == Weekdays.Monday && Weekdays.Of(4) == Weekdays.Thursday && Weekdays.Of(7) == Weekdays.Sunday
              && Weekdays.Of(8) == Weekdays.Monday && Weekdays.Of(11) == Weekdays.Thursday, "Day 1 is a Monday, Day 4 a Thursday, Day 8 the next Monday.");
        Check(Weekdays.Of(0) == -1 && Weekdays.Name(0) == "" && Weekdays.Label(0) == "Day 0", "Before the first day there is no weekday.");
        Check(Weekdays.Name(3) == "Wednesday" && Weekdays.Capital(5) == "FRIDAY" && Weekdays.Label(4) == "Thursday · Day 4",
            "The HUD and the recap say \"Thursday · Day 4\".");
        Check(Weekdays.IsThursday(4) && Weekdays.IsThursday(18) && !Weekdays.IsThursday(5) && !Weekdays.IsThursday(0), "Thursdays are days 4, 11, 18...");

        // ---------- her night by the clock ----------
        GraceNight.Step[] usual = GraceNight.Usual, thursday = GraceNight.Thursday;
        Check(GraceNight.For(1) == usual && GraceNight.For(3) == usual && GraceNight.For(4) == thursday && GraceNight.For(11) == thursday,
            "Six nights a week her usual night; the night after a Thursday, her odd one.");
        for (int i = 1; i < usual.Length; i++) Check(usual[i].at > usual[i - 1].at, "Her usual night's steps come in order.");
        for (int i = 1; i < thursday.Length; i++) Check(thursday[i].at > thursday[i - 1].at, "Her Thursday's steps come in order.");
        Check(usual[0].at == GraceNight.NightStarts && usual[0].act == GraceNight.Act.Sit, "At 11 PM she's in her armchair (the TV on).");
        Check(GraceNight.Clock(usual[1].at) == "11:35 PM" && usual[1].act == GraceNight.Act.Tea, "At 11:35 PM the kettle.");
        Check(GraceNight.Clock(usual[2].at) == "11:45 PM" && usual[2].act == GraceNight.Act.Sit, "At 11:45 PM back to her armchair.");
        Check(GraceNight.Clock(usual[3].at) == "12:00 AM" && usual[3].act == GraceNight.Act.Bed, "At midnight up to bed.");
        Check(GraceNight.Clock(usual[4].at) == "12:20 AM" && usual[4].act == GraceNight.Act.Sleep, "At 12:20 AM her lamp off: asleep.");
        Check(GraceNight.Clock(usual[5].at) == "2:40 AM" && usual[5].act == GraceNight.Act.Water, "At 2:40 AM a glass of water.");
        Check(GraceNight.Clock(usual[6].at) == "3:05 AM" && usual[6].act == GraceNight.Act.Sleep, "About 3:05 AM asleep again.");
        Check(thursday[0].act == GraceNight.Act.Out && thursday[1].act == GraceNight.Act.ComeHome
              && GraceNight.Clock(GraceNight.ThursdayHome) == "1:30 AM" && GraceNight.Clock(thursday[1].at) == "1:00 AM"
              && thursday[1].at < GraceNight.ThursdayHome,
            "Thursdays she's out, on her way home from 1:00 AM, and in at her door at 1:30 AM.");
        Check(GraceNight.Clock(thursday[2].at) == "1:50 AM" && thursday[2].act == GraceNight.Act.Bed
              && GraceNight.Clock(thursday[3].at) == "2:10 AM" && thursday[3].act == GraceNight.Act.Sleep,
            "Thursdays: bed at 1:50, asleep at 2:10.");
        Check(Array.FindIndex(thursday, s => s.act == GraceNight.Act.Water) < 0, "Thursdays there's no water at 2:40 (she has only just gone to bed).");
        Check(GraceNight.Now(usual, 22f) == 0 && GraceNight.Now(usual, 23f) == 0 && GraceNight.Now(usual, 23.5f) == 0
              && GraceNight.Now(usual, 23.6f) == 1 && GraceNight.Now(usual, 24.1f) == 3 && GraceNight.Now(usual, 26.7f) == 5
              && GraceNight.Now(usual, 28f) == 6, "The step under way is the last one begun.");
        Check(GraceNight.Now(thursday, 24f) == 0 && GraceNight.Now(thursday, 25.3f) == 1 && GraceNight.Now(thursday, 26.5f) == 3,
            "Thursdays: out until she sets off, then home, bed, asleep.");
        Check(GraceNight.Clock(23f) == "11:00 PM" && GraceNight.Clock(24.5f) == "12:30 AM" && GraceNight.Clock(28f) == "4:00 AM",
            "The clock reads like the HUD's.");

        // ---------- her eyes ----------
        float closeLit = GraceNight.SeeRate(true, .5f, 7f, 0f, 55f, false, false, false);
        Check(Near(1f / closeLit, 1f, .01f), $"Close up in a lit room her mark fills in about 1 s ({1f / closeLit:0.00} s).");
        float edgeLit = GraceNight.SeeRate(true, 6.9f, 7f, 0f, 55f, false, false, false);
        Check(Near(1f / edgeLit, 2.97f, .02f), $"At the edge of her view, about 3 s ({1f / edgeLit:0.00} s).");
        float side = GraceNight.SeeRate(true, 1f, 7f, 54f, 55f, false, false, false);
        Check(1f / side > 2.9f, "Out of the corner of her eye, about 3 s too.");
        float dark = GraceNight.SeeRate(false, .5f, 3f, 0f, 55f, false, false, false);
        Check(Near(1f / dark, 3f), "In a dark room, about 3 s.");
        Check(GraceNight.SeeRate(true, 7.5f, 7f, 0f, 55f, false, false, false) == 0f && GraceNight.SeeRate(false, 3.2f, 3f, 0f, 55f, false, false, false) == 0f,
            "She sees 7 m in a lit room, 3 m in a dark one, no further.");
        Check(GraceNight.SeeRate(true, 1f, 7f, 56f, 55f, false, false, false) == 0f, "Outside her 110° she sees nothing.");
        Check(Near(GraceNight.SeeRate(true, 1f, 7f, 0f, 55f, true, false, false), closeLitAt(1f) * .5f, 1e-4f), "Half as fast while Ace sneaks.");
        Check(Near(GraceNight.SeeRate(true, 1f, 7f, 0f, 55f, false, true, false), closeLitAt(1f) * 2f, 1e-4f), "Twice as fast with Ace's torch on.");
        Check(Near(GraceNight.SeeRate(true, 1f, 7f, 0f, 30f, false, false, true), closeLitAt(1f) * .5f, .01f), "Half as fast while she watches TV.");
        Check(GraceNight.Range(false, true) == GraceNight.LitRange && GraceNight.Range(true, false) == 7f && GraceNight.Range(false, false) == 3f,
            "Ace's torch lights Ace up: she sees it as far as in a lit room.");
        Check(GraceNight.Cone == 110f && GraceNight.TvCone == 60f, "Her view is 110° wide; watching TV, the 60° toward the screen.");

        // ---------- her mark ----------
        float mark = GraceNight.Heard(0f);
        Check(Near(mark, GraceNight.Third), "A sound she hears takes her mark to a third (\"Hm?\").");
        Check(Near(GraceNight.Heard(.1f), GraceNight.Third), "Under a third, any sound takes it to a third.");
        mark = GraceNight.Heard(mark);
        Check(Near(mark, .5f), "Another sound while she listens adds a sixth.");
        mark = GraceNight.Heard(GraceNight.Heard(GraceNight.Heard(mark)));
        Check(Near(mark, GraceNight.SoundCap), "Sounds alone never take her past two thirds: she has to see Ace to catch Ace.");
        Check(Near(GraceNight.Heard(.9f), .9f), "A sound never lowers it.");
        Check(GraceNight.Seen(.5f, 1f, .3f) > .79f && GraceNight.Seen(.9f, 1f, 1f) == 1f && GraceNight.Caught(GraceNight.Seen(.9f, 1f, 1f)),
            "Seeing Ace fills it, never past full; full is caught.");
        Check(Near(GraceNight.Drained(.5f, 1f), .3f) && GraceNight.Drained(.1f, 5f) == 0f, "It drains a fifth a second, to nothing.");
        Check(!GraceNight.Caught(.99f), "Nearly full isn't caught.");
        Check(GraceNight.SearchFrom > GraceNight.Third && GraceNight.SearchFrom < GraceNight.SoundCap,
            "Two sounds in a row (or a good look at Ace, then gone) bring her to look; one doesn't.");
        Check(GraceNight.WakeSeconds == 20f && GraceNight.LookSeconds == 2f, "Woken for 20 s; at a third she looks for 2 s.");

        // ---------- the ways she walks ----------
        var spots = GraceHouseMap.Spots;
        var path = new List<int>();
        for (int a = 0; a < spots.Length; a++)
            for (int b = 0; b < spots.Length; b++)
                Check(GraceHouseMap.Path(a, b, path) && path[0] == a && path[path.Count - 1] == b, $"From {spots[a].name} she can get to {spots[b].name}.");
        for (int a = 0; a < spots.Length; a++)
            foreach (int b in GraceHouseMap.Next(a))
            {
                Check(GraceHouseMap.Joined(b, a), "Every way goes both ways.");
                Check(Math.Abs(spots[a].Z - spots[b].Z) < .01f || IsStairs(a, b), $"Only the stairs climb ({spots[a].name} to {spots[b].name}).");
                Check(GraceHouseMap.Length(a, b) < 2.4f, $"No way is longer than 2.4 m ({spots[a].name} to {spots[b].name}).");
                Check(!CrossesTheStairsBlock(spots[a], spots[b]), $"No way on the ground floor crosses the stairs' block ({spots[a].name} to {spots[b].name}).");
            }
        Check(GraceHouseMap.Path(GraceHouseMap.Armchair, GraceHouseMap.Bedside, path) && path.Contains(GraceHouseMap.BottomStep)
              && path.Contains(GraceHouseMap.Landing) && path.Contains(GraceHouseMap.UpperFlightTop) && path.Contains(GraceHouseMap.BedroomDoorway),
            "From her armchair to her bed: up both flights and through the bedroom doors.");
        Check(GraceHouseMap.Path(GraceHouseMap.Armchair, GraceHouseMap.Kettle, path) && !path.Contains(GraceHouseMap.FootOfStairs)
              && GraceHouseMap.PathLength(GraceHouseMap.Armchair, GraceHouseMap.Kettle) < 3.6f,
            "From her armchair to the kettle: a few steps across the kitchen.");
        Check(GraceHouseMap.Path(GraceHouseMap.Bedside, GraceHouseMap.Sink, path) && path.Contains(GraceHouseMap.LowerFlightTop),
            "From her bed to the sink: down the stairs.");
        Check(GraceHouseMap.Nearest(2.30f, 2.00f, 0f) == GraceHouseMap.Pocket && GraceHouseMap.Nearest(1.30f, 3.55f, 2.4f) == GraceHouseMap.Wardrobe
              && GraceHouseMap.Nearest(2.30f, 2.00f, 2.4f) != GraceHouseMap.Pocket && GraceHouseMap.Nearest(3.0f, 2.0f, 9f) == -1,
            "The nearest spot is on the same floor (a point upstairs never finds the room under it).");
        Check(GraceHouseMap.Find("at the kettle") == GraceHouseMap.Kettle && GraceHouseMap.Find("nowhere") == -1, "Spots are found by name.");
        Check(spots[GraceHouseMap.Bedside].Storey == 1 && spots[GraceHouseMap.Landing].Storey == 0 && spots[GraceHouseMap.Kettle].Storey == 0,
            "Upstairs begins at the top of the upper flight.");
        return count;

        float closeLitAt(float d) => GraceNight.SeeRate(true, d, 7f, 0f, 55f, false, false, false);
    }

    // The flights and the landing between them: the only ways whose ends are at different heights.
    static bool IsStairs(int a, int b)
    {
        int[] stairs = { GraceHouseMap.BottomStep, GraceHouseMap.LowerFlightTop, GraceHouseMap.UpperFlightFoot, GraceHouseMap.UpperFlightTop };
        return Array.IndexOf(stairs, a) >= 0 && Array.IndexOf(stairs, b) >= 0;
    }

    // The block under the landing and the upper flight (the cupboard) stands at X 0-2.6, Y 0-1.42 on the ground floor; the
    // lower flight runs along the south wall at X 0-1.40 from Y 1.40 to 2.60 (its open side, the banister, at X 1.40-1.46).
    // A way on the ground floor (both ends below 0.5 m) mustn't pass through either.
    static bool CrossesTheStairsBlock(GraceHouseMap.Spot a, GraceHouseMap.Spot b)
    {
        if (a.Z > .5f || b.Z > .5f) return false;
        for (int i = 0; i <= 40; i++)
        {
            float t = i / 40f;
            float x = a.X + (b.X - a.X) * t, y = a.Y + (b.Y - a.Y) * t;
            if (x < 2.6f && y < 1.42f) return true;
            if (x > .3f && x < 1.46f && y > 1.40f && y < 2.60f) return true;
        }
        return false;
    }
}
