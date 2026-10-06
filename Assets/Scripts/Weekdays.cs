// ---------------------------------------------------------------------------
// THE DAYS OF THE WEEK (break-ins chunk C, 6 Oct 2026; Mansoor's call: "Weekdays from Day 1 = Monday")
//
// Until chunk C the game only counted days (Day 1, Day 2...). Grace keeps one odd night a week (claude/npc-life-plan.md
// §5: "fixed, with one odd day"; the man's page already says "Grace. Thursdays. Find out."), so the days need names:
// Day 1 is a Monday, Day 4 a Thursday, Day 8 the next Monday. Nothing is saved for it: the weekday follows from the
// day. The HUD, the recap and the night's captions name it ("Thursday · Day 4").
//
// No Unity types: Tests/GraceRules compiles this file.
// ---------------------------------------------------------------------------
public static class Weekdays
{
    public const int Monday = 0, Tuesday = 1, Wednesday = 2, Thursday = 3, Friday = 4, Saturday = 5, Sunday = 6;

    // Made once: the HUD asks again whenever its minute changes.
    static readonly string[] Names = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
    static readonly string[] Capitals = { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" };

    /// <summary>Which day of the week day <paramref name="day"/> is (0 Monday ... 6 Sunday); -1 before the first day.</summary>
    public static int Of(int day) => day <= 0 ? -1 : (day - 1) % 7;

    /// <summary>"Thursday" for day 4; "" before the first day.</summary>
    public static string Name(int day)
    {
        int w = Of(day);
        return w < 0 ? "" : Names[w];
    }

    /// <summary>"THURSDAY" for day 4 (the recap's kicker); "" before the first day.</summary>
    public static string Capital(int day)
    {
        int w = Of(day);
        return w < 0 ? "" : Capitals[w];
    }

    public static bool IsThursday(int day) => Of(day) == Thursday;

    /// <summary>"Thursday · Day 4" (just "Day 0" before the first day).</summary>
    public static string Label(int day)
    {
        string name = Name(day);
        return name.Length == 0 ? "Day " + day : name + " · Day " + day;
    }
}
