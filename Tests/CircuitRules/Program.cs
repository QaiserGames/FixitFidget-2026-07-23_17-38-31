using System;

internal static class Program
{
    private static int Main()
    {
        try
        {
            Console.WriteLine($"Circuit rules PASS: {CircuitRuleChecks.RunAll()} assertions.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
