using System;
using System.CommandLine;

namespace AzureFunctions;

public class AzureFunctionsProfiling
{
    // Enum to define the ways this little tool can be run:
    //
    // - Run: Will only run the 'func-harness' benchmark and capture the trace.
    // - Analyze: Will receive an existing trace and pass it to the profiler tool.
    // - All: Will run 'func-harness', capture the trace, and pass said trace to
    //        the profiler tool.

    [Flags]
    internal enum Modes
    {
        Run,
        Analyze,
        All = Run | Analyze
    }

    private static Modes s_modeToRun = Modes.All;

    // This is the relative path to the Powershell script that runs the benchmark.
    // We might end up changing it to be embedded here. Maybe...
    private const string s_runScript = "AzureFunctionsProfiling-Windows.ps1";

    /// <summary>Little tool to run and profile Azure Functions benchmarking.</summary>
    /// <param name="mode">Select between running a benchmark, analyzing a trace, or both.</param>
    static int Main(string mode = "All")
    {
        if (!Enum.TryParse(mode, out s_modeToRun))
        {
            Console.WriteLine($"Apologies, but the passed mode '{mode}' is not recognized.");
            Console.WriteLine("The available values are Run, Analyze, All");
            return 1;
        }

        // Steps:
        // 1) Run the script app.
        // 2) Run the profiling tool.
        // 3) Analyze further if needed.
        return 0;
    }
}
