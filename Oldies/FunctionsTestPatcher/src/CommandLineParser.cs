// File: CommandLineParser.cs

using System;
using System.Collections.Generic;

namespace AzureFunctions;

internal class MainContext
{
    public string FunctionsRepo { get; set; }
    public string PatchedSdk { get; set; }
    public string WorkZone { get; set; }
}

internal static class CommandLineParser
{
    public static MainContext ParseToContext(string[] args)
    {
        var ctx = new MainContext();
        var argsList = new List<string>(args);

        while (argsList.Count > 0)
        {
            string nextArg = argsList[0];

            switch (nextArg)
            {
                case "--functions":
                case "--functions-repo":
                    ctx.FunctionsRepo = ParseArg(argsList);
                    break;

                case "--sdk":
                case "--patched-sdk":
                    ctx.PatchedSdk = ParseArg(argsList);
                    break;

                case "--work":
                case "--work-zone":
                    ctx.WorkZone = ParseArg(argsList);
                    break;

                default:
                    throw new ArgumentException($"The parameter {nextArg} was"
                                                + " not recognized :(");
            }
        }
        return ctx;
    }

    private static string ParseArg(List<string> args)
    {
        if (args.Count == 1 || args[1].StartsWith("--"))
        {
            args.RemoveAt(0);
            return string.Empty;
        }

        // Remove [0] twice because it behaves like a Linked List. Once you remove
        // the first [0], then [1] automatically becomes a new [0].

        string result = args[1];
        args.RemoveAt(0);
        args.RemoveAt(0);
        return result;
    }
}
