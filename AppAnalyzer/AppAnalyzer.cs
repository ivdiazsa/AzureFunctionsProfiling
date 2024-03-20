using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace AzureFunctionsProfiling;

public class AppAnalyzer
{
    // I created this class just as a funny experiment, but we don't need it right now.
    // I'm leaving it here as a comment, just in case we want to use it later.

    /*
    internal class JITData
    {
        public readonly string Name;
        public readonly float TimeMsec;
        public readonly int Count;

        public JITData(string name, string time, string count)
        {
            Name = name;

            if (!Double.TryParse(time, out TimeMsec))
                Console.WriteLine($"Failed to parse JIT Time '{time}' :(");

            // In the coldstart file, the JIT count comes in a string with the
            // form of '(count:XYZ)'.
            string[] countStrParts = count.Split(":");

            if (!Int32.TryParse(countStrParts[1].TrimEnd(")"), out Count))
                Console.WriteLine($"Failed to parse JIT count '{count}' :(");
        }
    }
    */

    const string coldStartExtension = ".coldstart";
    const string jitTimeString = "JIT time during specialization";
    const string detailedJitString = "Detailed JIT Times:";

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

    static void Main(string[] args)
    {
        string path = args[0];
        // ExtractInfoFromProfile(path, new string[] {"detailed-jit"});

        string[] contents = File.ReadAllLines(path);
        string[] headers = new string[] { "Jitted Method", "Time" };
        int[] sizes = { -1, -1 };

        TableArranger table = new TableArranger(contents, headers, sizes, " : ");
        table.DisplayTable();
    }

    static int Main2(string[] args)
    {
        string analyzerExePath = args[0];
        string traceFullPath = args[1];

        // The third and so on elements of the args array are the options that
        // are to be explored in the analysis. Hence, them being there means we
        // will need to do further processing after running the Azure Functions
        // profiler app.

        bool willAnalyzeFurther = (args.Length > 2);
        int aec = RunAnalyzer(analyzerExePath, traceFullPath, willAnalyzeFurther);

        if (aec != 0)
        {
            Console.WriteLine("\nSomething went wrong with the analyzer tool :(");
            Console.WriteLine($"EXIT CODE: {aec}\n");
            return aec;
        }

        Console.WriteLine($"\nEXIT CODE: {aec}\n");

        if (willAnalyzeFurther)
        {
            string[] metrics = args[2..];
            ExtractInfoFromProfile(traceFullPath + coldStartExtension, metrics);
        }

        return 0;
    }

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
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            toolRunner.StartInfo = psi;
            toolRunner.Start();

            // If we will further analyze and extract data, then there is no
            // need to flood the user's terminal with the analyzer tool output.

            if (!willAnalyzeFurther)
            {
                while (!toolRunner.StandardOutput.EndOfStream)
                {
                    string line = toolRunner.StandardOutput.ReadLine();
                    Console.WriteLine(line);
                }
            }

            toolRunner.WaitForExit();
            exitCode = toolRunner.ExitCode;
        }
        return exitCode;
    }

    // **********************************************************************
    // ExtractInfoFromProfile(): This function reads the received .coldstart
    // file, and looks up the information requested by means of the received
    // metrics flags. Then, it prints it to console.
    // **********************************************************************

    static void ExtractInfoFromProfile(string coldStartFile,
                                       string[] metrics)
    {
        string[] lines = File.ReadAllLines(coldStartFile);

        foreach (string m in metrics)
        {
            switch (m)
            {
                case "jit-time":
                    JitTimeAndCount(lines);
                    break;

                case "detailed-jit":
                    DetailedJitTimes(lines);
                    break;

                default:
                    Console.WriteLine($"\nApologies, but metric {m} is not (yet)"
                                      + " supported.");
                    break;
            }
        }
    }

    // ***************************************************************************
    // JitTimeAndCount(): This function checks the lines of the profile log file,
    // and parses them to retrieve the number of JIT methods and the time spent
    // there in milliseconds (msec).
    // ***************************************************************************

    private static void JitTimeAndCount(string[] profileLines)
    {
        string[] jitTimeLines = Array.FindAll(profileLines,
                                              x => x.Contains(jitTimeString));
        if (jitTimeLines.Length < 1)
        {
            Console.WriteLine($"\nApologies, but there were no lines"
                              + " found matching '{jitTimeString}' :(");
            return ;
        }

        foreach (string line in jitTimeLines)
        {
            // Each jit time line comes in the format of:
            // "<Whose> JIT time during specialization msec: abc.def (count:XYZ)"
            string[] words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // "<Whose>" may be compose of one or more words, so the way to know
            // it for the name is to find the word "JIT", since it is guaranteed
            // it will be the word after the name.
            string name = String.Join(' ', words[0..Array.IndexOf(words, "JIT")]);

            // In the coldstart file, the JIT count comes in a string with the
            // form of '(count:XYZ)'.
            string count = words[^1].Split(':')[^1].TrimEnd(')');
            string msec = words[^2];

            Console.WriteLine($"\n{new string('*', name.Length)}");
            Console.WriteLine(name);
            Console.WriteLine($"{new string('*', name.Length)}");
            Console.WriteLine($"JIT Time Msec: {msec}");
            Console.WriteLine($"JIT Count: {count}");
        }
    }

    // ********************************************************************************
    // DetailedJitTimes(): This function retrieves the list of all the Jitted methods,
    // and prints them out in a table. This table is custom made and the code is in
    // the TableArranger class.
    // ********************************************************************************

    private static void DetailedJitTimes(string[] profileLines)
    {
        Console.WriteLine("\n****************************");
        Console.WriteLine("All Jitted Methods and Times");
        Console.WriteLine("****************************");

        // The list of jitted methods and their times starts at least one line
        // after the label "Detailed JIT Times:".
        int jitTimesStart = Array.IndexOf(profileLines, detailedJitString);
        while (String.IsNullOrWhiteSpace(profileLines[++jitTimesStart])) ;

        int index = jitTimesStart;
        List<string> jittedMethodsList = new List<string>();

        // Store all the Jitted Methods information, so that we can afterwards
        // pass it to the table maker.
        while (!String.IsNullOrWhiteSpace(profileLines[index]))
        {
            jittedMethodsList.Add(profileLines[index]);
            index++;
        }

        TableArranger table = new TableArranger(
            entries: jittedMethodsList.ToArray(),
            headers: new string[] { "Jitted Methods", "Jitting Time" },
            lengths: new int[] { -1, -1 },
            rawDelimiter: " : ");

        table.DisplayTable();
    }
}
