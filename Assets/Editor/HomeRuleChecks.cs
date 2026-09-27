using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

// Rule checks for homes (claude/night-homes-spec.md §5): who walks which way,
// how sure a sighting makes Ace, street names, and the recap lines. Pure: no
// scene, no save file, no assets. Also compiled by Tests/HomeRules.
public static class HomeRuleChecks
{
#if UNITY_EDITOR
    [MenuItem("Fixit Fidget/Checks/Home rules")]
    public static void Run()
    {
        try
        {
            int count = RunAll() + CheckUnityJson();
            Debug.Log("[Home rules] PASS: " + count + " assertions. No scene, save or asset changes.");
        }
        finally
        {
            // The checks rename streets; put the real names back.
            DistrictStreets.LoadFromResources();
        }
    }

    // Unity's own serializer, which is what the real save uses.
    static int CheckUnityJson()
    {
        int n = 0;
        void Check(bool ok, string why) { n++; if (!ok) throw new InvalidOperationException(why); }
        var book = new Notebook();
        book.Learn(NotebookEntries.HomeSeen("grace", "Grace", "the saffron house", "12", "west", true, Notebook.Sureness.Hunch), 2);
        book.Learn(NotebookEntries.HomeSeen("grace", "Grace", "the saffron house", "12", "west", false, Notebook.Sureness.Likely), 3);
        var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { day = 3, notebook = book.Snapshot() }));
        back.ValidateAndMigrate();
        var again = new Notebook();
        again.Restore(back.notebook);
        NotebookFactData home = again.Find("grace.home");
        Check(home != null && home.sure == Notebook.Sureness.Likely && home.surerDay == 3 && home.day == 2 && home.text.Contains("{street:west}"),
            "A sighting survives Unity's JSON, with the day Ace became surer and the street still a token.");
        var old = JsonUtility.FromJson<SaveData>("{\"version\":5,\"day\":4,\"notebook\":[{\"id\":\"grace.camera.strap\",\"who\":\"grace\",\"sure\":\"sure\",\"day\":1}]}");
        old.ValidateAndMigrate();
        Check(old.notebook.Length == 1 && old.notebook[0].surerDay == 0, "A notebook saved before homes loads with no 'surer' day.");
        return n;
    }
#endif

    public static int RunAll()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new InvalidOperationException(why); }
        int Count(string s, string part)
        {
            int n = 0;
            for (int at = s.IndexOf(part, StringComparison.Ordinal); at >= 0; at = s.IndexOf(part, at + part.Length, StringComparison.Ordinal)) n++;
            return n;
        }

        // ---------- street names ----------
        StreetNames.Clear();
        Check(StreetNames.Token("west") == "{street:west}", "A street is written as a token.");
        Check(StreetNames.Name("west") == "the west street" && StreetNames.Resolve("On {street:west}.") == "On the west street.",
            "A street with no name yet still reads naturally.");
        StreetNames.Set("west", "West Street");
        StreetNames.Set("east", "East Street");
        Check(StreetNames.Resolve("12 {street:west}, by {street:east}.") == "12 West Street, by East Street.", "Every token is filled in.");
        Check(StreetNames.Resolve("{street:WEST}") == "West Street", "Ids ignore case.");
        Check(StreetNames.Resolve("No streets here.") == "No streets here." && StreetNames.Resolve(null) == "" && StreetNames.Resolve("{street:west") == "{street:west",
            "Text without a whole token is left alone.");
        StreetNames.Set("west", "Maple Lane");
        Check(StreetNames.Resolve("12 {street:west}") == "12 Maple Lane", "Renaming a street renames it everywhere it is shown.");
        StreetNames.Set("west", "   ");
        Check(StreetNames.Name("west") == "the west street", "A blank name falls back rather than leaving a gap.");
        StreetNames.Set("west", "West Street");

        // ---------- who walks which way ----------
        string[] homes = { "home.grace", "", "" };
        int[] points = { 9, 11, 10 };
        float[] weights = { 1f, 1f, 1f };
        Check(HomeRules.HomeRoute("home.grace", homes, points) == 0, "Grace gets her own front door's route.");
        Check(HomeRules.HomeRoute(" home.grace ", homes, points) == 0, "…however the id is spaced.");
        Check(HomeRules.HomeRoute("", homes, points) == -1 && HomeRules.HomeRoute(null, homes, points) == -1,
            "No home, no home route: they arrive like anyone else.");
        Check(HomeRules.HomeRoute("home.tomas", homes, points) == -1, "A home with no route of its own falls back to the usual arrivals.");
        Check(HomeRules.HomeRoute("home.grace", homes, new[] { 1, 11, 10 }) == -1, "A route with no path is not a way home.");
        var picked = new HashSet<int>();
        for (int i = 0; i < 1000; i++) picked.Add(HomeRules.PublicRoute(homes, weights, points, i / 1000.0));
        Check(!picked.Contains(0) && picked.SetEquals(new[] { 1, 2 }), "Walk-ins never use a home's door; both public routes get used.");
        Check(HomeRules.PublicRoute(homes, weights, points, 0) == 1 && HomeRules.PublicRoute(homes, weights, points, .99) == 2,
            "The roll picks between the public routes by weight.");
        Check(HomeRules.PublicRoute(homes, new[] { 1f, 0f, 1f }, points, .2) == 2, "A route with no weight is never picked.");
        Check(HomeRules.PublicRoute(new[] { "home.grace" }, new[] { 1f }, new[] { 9 }, .5) == -1,
            "With only home routes, walk-ins have nowhere to come from (and start at the door as before).");
        Check(HomeRules.PublicRoute(homes, new[] { 1f, 0f, 0f }, points, .5) == -1, "No weight at all: no route.");

        // ---------- sightings ----------
        var book = new Notebook();
        string Sure(int day) => HomeRules.SightingSureness(book.Find("grace.home"), day);
        NotebookFactData Seen(bool cameOut, int day) =>
            NotebookEntries.HomeSeen("grace", "Grace", "the saffron house", "12", "west", cameOut, Sure(day));
        Check(Sure(2) == Notebook.Sureness.Hunch, "Never seen before: a hunch.");
        NotebookFactData first = Seen(true, 2);
        Check(first.id == "grace.home" && first.kind == Notebook.Kinds.Address && first.source == Notebook.Sources.Seen
              && first.text == "Came out of the saffron house at 12 {street:west}.",
            "The first sighting: an address, seen, in Ace's shorthand, the street as a token.");
        Check(book.Learn(first, 2) && book.Find("grace.home").sure == Notebook.Sureness.Hunch, "It goes in as a hunch.");
        Check(!book.Learn(Seen(false, 2), 2) && book.Find("grace.home").sure == Notebook.Sureness.Hunch
              && book.Find("grace.home").confirmedDay == 0 && book.Count == 1,
            "Seeing her go back in the same day adds nothing.");
        Check(Sure(3) == Notebook.Sureness.Likely, "Seen again on a later day: likely.");
        book.Learn(Seen(false, 3), 3);
        NotebookFactData home = book.Find("grace.home");
        Check(home.sure == Notebook.Sureness.Likely && home.confirmedDay == 3 && home.surerDay == 3 && home.day == 2 && book.Count == 1,
            "…confirmed, and Ace became surer on that day; still one fact.");
        Check(home.text.StartsWith("Came out of", StringComparison.Ordinal), "What was written first stays written.");
        book.Learn(Seen(true, 5), 5);
        Check(book.Find("grace.home").sure == Notebook.Sureness.Likely && book.Find("grace.home").surerDay == 3 && book.Find("grace.home").confirmedDay == 5,
            "Sightings stop at likely: sure is kept for something stronger.");
        Check(NotebookEntries.HomeSeen("", "Nobody", "a house", "1", "west", true, Notebook.Sureness.Hunch) == null, "Nobody, no fact.");
        Check(NotebookEntries.HomeSeen("tomas", "Tomas", "the blue house", "", "east", false, "").text == "Went into the blue house on {street:east}."
              && NotebookEntries.HomeSeen("tomas", "Tomas", "the blue house", "", "east", false, "").sure == Notebook.Sureness.Hunch,
            "Without a house number: the street only; no sureness given: a hunch.");
        Check(NotebookEntries.HomeSeen("tomas", "Tomas", "", "3", "", true, "").text == "Came out of a house.", "Without a street: just the house.");

        // ---------- the recap ----------
        var recap = new Notebook();
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake("Grace")) recap.Learn(fact, 1);
        recap.Learn(NotebookEntries.HomeSeen("grace", "Grace", "the saffron house", "12", "west", true, Notebook.Sureness.Hunch), 2);
        string day2 = NotebookRecap.Build(recap, 2);
        Check(day2.Contains("1 new") && !day2.Contains("surer")
              && day2.Contains("Grace: came out of the saffron house at 12 West Street. <color=#A6A6A6>(hunch)</color>"),
            "Day 2: the sighting is new, with its street named and marked as a hunch.");
        Check(!NotebookRecap.Build(recap, 1).Contains("(hunch)") && !NotebookRecap.Build(recap, 1).Contains("(sure"),
            "Things Ace was told carry no mark.");
        StreetNames.Set("west", "Maple Lane");
        Check(NotebookRecap.Build(recap, 2).Contains("at 12 Maple Lane."), "Rename the street and the notebook says the new name.");
        StreetNames.Set("west", "West Street");
        recap.Learn(NotebookEntries.HomeSeen("grace", "Grace", "the saffron house", "12", "west", false,
            HomeRules.SightingSureness(recap.Find("grace.home"), 4)), 4);
        string day4 = NotebookRecap.Build(recap, 4);
        Check(day4.StartsWith("<b>Notebook</b>   1 surer", StringComparison.Ordinal) && !day4.Contains("new")
              && day4.Contains("Grace: came out of the saffron house at 12 West Street. <color=#A6A6A6>(likely now)</color>"),
            "Day 4: nothing new, but Ace is surer: the block says so, and why.");
        Check(day4.Contains("(5 facts about 1 person so far)"), "The running total counts the sighting once.");
        recap.Learn(NotebookEntries.HomeSeen("grace", "Grace", "the saffron house", "12", "west", true,
            HomeRules.SightingSureness(recap.Find("grace.home"), 6)), 6);
        Check(NotebookRecap.Build(recap, 6) == "", "A third sighting changes nothing, so no block.");
        recap.Learn(NotebookEntries.RegularRepair("tomas", "Tomas", "Phone", "Cracked Screen"), 4);
        day4 = NotebookRecap.Build(recap, 4);
        Check(day4.Contains("1 new, 1 surer") && day4.IndexOf("Tomas:", StringComparison.Ordinal) < day4.IndexOf("(likely now)", StringComparison.Ordinal),
            "New facts come first, then the ones Ace became surer of.");
        Check(Count(day4, "<b>") == Count(day4, "</b>") && Count(day4, "<size=") == Count(day4, "</size>")
              && Count(day4, "<color=") == Count(day4, "</color>"), "The recap's rich-text tags are balanced.");
        Check(day4.Split('\n').Skip(1).All(line => line.StartsWith("<size=", StringComparison.Ordinal) && line.EndsWith("</size>", StringComparison.Ordinal)),
            "Every line is sized on its own.");
        var many = new Notebook();
        for (int i = 0; i < 4; i++) many.Learn(new NotebookFactData { id = "new." + i, who = "p" + i, name = "P" + i, text = "Fact " + i + "." }, 7);
        for (int i = 0; i < 3; i++)
        {
            many.Learn(new NotebookFactData { id = "old." + i, who = "q" + i, name = "Q" + i, text = "Old " + i + ".", sure = Notebook.Sureness.Hunch }, 5);
            many.Learn(new NotebookFactData { id = "old." + i, sure = Notebook.Sureness.Likely }, 7);
        }
        string busy = NotebookRecap.Build(many, 7);
        Check(busy.Contains("4 new, 3 surer") && busy.Contains("+2 more") && Count(busy, "\n") == NotebookRecap.MaxLines + 2,
            "A busy day still shows five lines, then how many more.");

        StreetNames.Clear();
        return count;
    }
}
