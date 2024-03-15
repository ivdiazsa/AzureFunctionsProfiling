using System;
using System.Diagnostics;

namespace AzureFunctionsProfiling;

public class AppAnalyzer
{
    /*
      This analyzing and profiling app is expected to be called from the main
      Powershell script 'AzureFunctionsProfiling.ps1', which is located at the
      root of the repo. That script already does all the necessary validation
      to ensure the parameters we have are correct and we can work with them.
      Hence, we're not adding extra validation here for that. Additionally,
      the script does some cwd changes, which it reverts at the end.

      The arguments we expect for this app are the following:

      - args[0]: Profiler tool exe
      - args[1]: Trace path
      - args[2..]: Options for parsing and analyzing the coldstart profile result.
     */

    static int Main(string[] args)
    {
        string analyzerExePath = args[0];
        string traceFullPath = args[1];

        // The third and so on elements of the args array are the options that
        // are to be explored in the analysis. Hence, them being there means we
        // will need to do further processing after running the Azure Functions
        // profiler app.

        bool willAnalyzeFurther = (args.Length > 2);
        int aec = RunAnalyzer(analyzerExePath, traceFullPath, willAnalyzeFurther);
        Console.WriteLine($"\nEXIT CODE: {aec}\n");
        return aec;
    }

    // **********************************************************************
    // RunAnalyzer(): This function runs the Azure Functions profiler app to
    //                analyze the given trace.
    // **********************************************************************

    static int RunAnalyzer(string analyzerExePath,
                           string traceFullPath,
                           bool willAnalyzeFurther = false)
    {
        int exitCode = 0;

        using (Process toolRunner = new Process())
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                Arguments = traceFullPath,
                CreateNoWindow = true,
                FileName = analyzerExePath,
                UseShellExecute = false,

                // If we will further analyze and extract data, then there is no
                // need to flood the user's terminal with the analyzer tool output.
                RedirectStandardOutput = willAnalyzeFurther
            };

            toolRunner.StartInfo = psi;
            toolRunner.Start();
            toolRunner.WaitForExit();

            exitCode = toolRunner.ExitCode;
        }
        return exitCode;
    }
}
