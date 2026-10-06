using System;

// Grace at home (break-ins chunk C): the days of the week, her night by the clock, her mark, and the ways she walks in
// her house. The in-Editor menu Fixit Fidget > Checks > Grace at home rules runs the same checks.
internal static class Program
{
    private static int Main()
    {
        try
        {
            int count = GraceRuleChecks.RunAll();
            Console.WriteLine($"Grace at home PASS: {count} assertions. Unity integration not run.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
