using System;
internal static class Program
{
    private static int Main()
    {
        try { Console.WriteLine($"Support call rules PASS: {HoldCallRuleChecks.RunAll()} assertions."); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
