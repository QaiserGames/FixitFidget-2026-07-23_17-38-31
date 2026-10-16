using System;
internal static class Program
{
    private static int Main()
    {
        try { Console.WriteLine($"Bench rules PASS: {BenchRules.SelfCheck()} assertions."); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
