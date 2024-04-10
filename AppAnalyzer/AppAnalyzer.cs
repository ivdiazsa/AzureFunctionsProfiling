using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace AzureFunctionsProfiling;

public class AppAnalyzer
{
    // Little class to keep track of the stats of a specific Jitted method.
    // Currently used for the condensed JIT stats.
    private class JittedMethodInfo
    {
        public string Name { get; init; }
        public double TimeMsec { get; set; }
        public int FoundCount { get; set; }

        public JittedMethodInfo(string name, double msec)
        {
            Name = name;
            TimeMsec = msec;
            FoundCount = 1;
        }

        public string ToTableCSVFormat()
        {
            return $"{Name} , {TimeMsec} , {FoundCount}";
        }
    }

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
        Console.WriteLine($"\nRunning {analyzerExePath} {traceFullPath}...");

        using (Process toolRunner = new Process())
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                Arguments = traceFullPath,
                CreateNoWindow = true,
                FileName = analyzerExePath,
                RedirectStandardOutput = false,
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
        Console.WriteLine($"Analyzing Cold Start Profile {coldStartFile}...");
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

                case "condensed-jit":
                    CondensedJitTimes(lines);
                    break;

                default:
                    Console.WriteLine($"\nApologies, but metric {m} is not (yet)"
                                      + " supported.\n");
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
                              + " found matching '{jitTimeString}' :(\n");
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

    private static void DetailedJitTimes(string[] profileLines, string delimiter = " : ")
    {
        Console.WriteLine("\n****************************");
        Console.WriteLine("All Jitted Methods and Times");
        Console.WriteLine("****************************\n");

        string[] allMethods = GetJittedMethodsList(profileLines);

        TableArranger table = new TableArranger(
            entries: allMethods,
            headers: new string[] { "Jitted Methods", "Jitting Time" },
            lengths: new int[] { 80, 30 },
            rawDelimiter: delimiter);

        table.DisplayTable();
    }

    // ****************************************************************************
    // SummarizedJitTimes(): In JIT profiles and traces, it is not uncommon to see
    // methods jitted more than once. This function retrieves the whole list like
    // DetailedJitTimes() above does, but instead of printing it as is, it will
    // only print each method once. It will show a counter of how many times it was
    // found, and the total time spent on that method, with all occurrences
    // aggregated together.
    // ****************************************************************************

    private static void CondensedJitTimes(string[] profileLines, string delimiter = " : ")
    {
        Console.WriteLine("\n************************************************");
        Console.WriteLine("Condensed Jitted Methods, Times, and Occurrences");
        Console.WriteLine("************************************************\n");

        string[] allMethods = GetJittedMethodsList(profileLines);

        // Use the classic Dictionary to keep track of how many times we've found
        // each method, as well as the total time all their instances took. Then,
        // construct a string array with each entry joined by the delimiter being
        // an element. I know this is highly inefficient and it's high on my list
        // of improvements to make to my TableArranger class: Be able to also
        // receive 2D list-like objects as inputs.

        var methodsData = new Dictionary<string, JittedMethodInfo>();

        foreach (string jm in allMethods)
        {
            string[] jmFields = jm.Split(delimiter);
            string jmName = jmFields[0];
            JittedMethodInfo jmInfo = null;

            // The Azure Functions profiling tool should guarantee we will be
            // getting a valid floating-point number.
            double jmMsec = Double.Parse(jmFields[1]);

            if (methodsData.TryGetValue(jmName, out jmInfo))
            {
                // Update the previously acquired information by adding this method's
                // instance's time. Also, increase the found count +1.
                jmInfo.TimeMsec += jmMsec;
                jmInfo.FoundCount++;
            }
            else
            {
                // Otherwise, add it to the dictionary.
                methodsData.Add(jmName, new JittedMethodInfo(jmName, jmMsec));
            }
        }

        // Convert our dictionary to a string array that the TableArranger class
        // can understand.
        string[] forTable = new string[methodsData.Count];
        int index = 0;

        foreach (JittedMethodInfo jmi in methodsData.Values)
        {
            forTable[index++] = jmi.ToTableCSVFormat();
        }

        TableArranger table = new TableArranger(
            entries: forTable,
            headers: new string[] { "Jitted Methods", "Total Msec", "Total Count" },
            lengths: new int[] { 80, 30, 5 },
            rawDelimiter: " , ");

        table.DisplayTable();
    }

    private static string[] GetJittedMethodsList(string[] profileLines)
    {
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

        return jittedMethodsList.ToArray();
    }
}
