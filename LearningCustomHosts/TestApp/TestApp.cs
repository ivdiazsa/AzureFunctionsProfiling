using System;

namespace TestNamespace;

public static class TestApp
{
    public static int MainEntryPoint(IntPtr arg, int argLength)
    {
        Console.WriteLine("\nHello from Main!");
        return 3;
    }

    public static int DifferentEntryPoint(IntPtr arg, int argLength)
    {
        Console.WriteLine("\nHello from Other Entry Point!");
        return 9;
    }

    public static int MagicSevens(IntPtr arg, int argLength)
    {
        Console.WriteLine("\nHello from Magic Sevens!");
        return 7;
    }
}
