using System;

// The bark rules outside Unity: Fixit Fidget > Checks > Bark rules runs the same checks in the Editor, plus
// the Night lines asset.
internal static class Program
{
    private static int Main()
    {
        try
        {
            int rules = BarkRuleChecks.RunAll();
            Console.WriteLine($"Barks PASS: {rules} rule assertions. Unity integration not run.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
