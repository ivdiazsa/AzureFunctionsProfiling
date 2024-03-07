using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace FunctionsColdStartProfileAnalyzer
{
    class TraceAnalyzer
    {
        private readonly string pgoColdStartFileLocation = Environment.ExpandEnvironmentVariables(@"%SYSTEMDRIVE%\devtools\PerfView\");
        private Regex languageWorkerPidMatch = new Regex(@".* process with Id=(.*) started", RegexOptions.Compiled);

        private string appName = string.Empty;
        private string activityId = string.Empty;
        private string functionsHostVersion = string.Empty;
        private int httpStatus = 0;

        private Dictionary<string, double> individualJitMethods = new Dictionary<string, double>();
        private Dictionary<string, double> detailedJitMethods = new Dictionary<string, double>();
        private Dictionary<string, double> dwasIndividualJitMethods = new Dictionary<string, double>();
        private Dictionary<string, double> dwasDetailedJitMethods = new Dictionary<string, double>();
        private Dictionary<string, double> diskReadFiles = new Dictionary<string, double>();
        private Dictionary<int, double> gcCounts = new Dictionary<int, double>();
        private Dictionary<int, double> dawsGCCounts = new Dictionary<int, double>();
        private Dictionary<Tuple<string, int>, int> activeProcesses = new Dictionary<Tuple<string, int>, int>();
        private Dictionary<string, string> processInfo = new Dictionary<string, string>();
        private Dictionary<string, Tuple<int, double>> memoryHardFaults = new Dictionary<string, Tuple<int, double>>();
        private List<string> networkShareFileNames = new List<string>();
        private List<Tuple<string, long>> dwasOutboundCalls = new List<Tuple<string, long>>();

        private string dwasColdStartPerfData = string.Empty;

        private int functionsHostPid = 0;
        private int dwasPid = 0;
        private double jitTime = 0;
        private double dwasJitTime = 0;
        private double gcTime = 0;
        private double dwasGCTime = 0;
        private double diskReadTime = 0;
        private double memoryHardFaultTime = 0;
        private long totalCpuSamples = 0;
        private long totalDwasOutboundCallsTime = 0;
        private int totalDwasProvisioningTime = 0;

        private long gcAllocationInBytes = 0;
        private long dwasGCAllocationInBytes = 0;

        double relativeStartTimeStamp = double.MaxValue;
        double relativeEndfTimeStamp = double.MaxValue;

        private int languageWorkerPid = 0;
        private double languageWorkerJitTime = 0;
        private double languageWorkerAssemblyLoaderTime = 0;
        private double languageWorkerTypeLoadTime = 0;
        private double languageWorkerGCTime = 0;
        private double languageWorkerMemoryHardFaultTime = 0;
        private Dictionary<int, double> languageWorkerGCCounts = new Dictionary<int, double>();
        private Dictionary<string, double> languageWorkerIndividualJitMethods = new Dictionary<string, double>();
        private Dictionary<string, double> languageWorkerDetailedJitMethods = new Dictionary<string, double>();
        private Dictionary<string, double> languageWorkerIndividualAssemblyLoaderMethods = new Dictionary<string, double>();
        private Dictionary<string, double> languageWorkerDetailedAssemblyLoaderMethods = new Dictionary<string, double>();
        private Dictionary<string, double> languageWorkerIndividualTypeLoads = new Dictionary<string, double>();
        private Dictionary<string, double> languageWorkerDetailedTypeLoads = new Dictionary<string, double>();
        private Dictionary<string, Tuple<int, double>> languageWorkerMemoryHardFaults = new Dictionary<string, Tuple<int, double>>();

        private double functionsHostCpuTime = 0;
        private double languageWorkerCpuTime = 0;
        private double dwasCpuTime = 0;

        public int Analyze(string outputFileName, string urlPattern, string etlFileName)
        {
            using (var traceLog = TraceLog.OpenOrConvert(etlFileName, new TraceLogOptions { KeepAllEvents = true }))
            {
                ProcessEvents(urlPattern, traceLog);

                if (relativeStartTimeStamp == double.MaxValue)
                {
                    var message = "No IIS event found";
                    Console.Error.WriteLine(message);
                    FunctionsColdStartAnalyzerEventSource.Instance.LogIISEventNotFound(etlFileName, message);
                    return -1;
                }

                if (functionsHostPid == 0)
                {
                    // This is just a 2nd best effort, this might be seen on private stamps for PGO.
                    // This method will not detect some dekstop CLR events but better than nothing.
                    var pidFoundAlternate = GetPidAlternateMethod(urlPattern, traceLog);
                    ClearAll();

                    functionsHostPid = pidFoundAlternate;
                    if (functionsHostPid != 0)
                    {
                        ProcessEvents(urlPattern, traceLog);
                    }
                }

                double coldStartMsec = relativeEndfTimeStamp - relativeStartTimeStamp;
                var output = new StringBuilder();

                StringBuilder activeProcessesOutput = new StringBuilder();
                foreach (var item in activeProcesses.OrderByDescending(key => key.Value))
                {
                    string commandLine = string.Empty;
                    string processDetail = item.Key.Item1 + "(" + item.Key.Item2 + ")";

                    if(item.Key.Item2 == functionsHostPid)
                    {
                        functionsHostCpuTime = item.Value;
                    }
                    else if (item.Key.Item2 == dwasPid)
                    {
                        dwasCpuTime = item.Value;
                    }
                    else if (item.Key.Item2 == languageWorkerPid)
                    {
                        languageWorkerCpuTime = item.Value;
                    }

                    if (processInfo.ContainsKey(processDetail))
                    {
                        commandLine = processInfo[processDetail];
                    }

                    var output1 = $"{ processDetail } : { Math.Round(((double)item.Value / totalCpuSamples) * 100, 2)}%";
                    activeProcessesOutput.AppendLine($"{output1.PadRight(50, ' ')}, {commandLine}");
                }

                StringBuilder networkShareFileNamesOutput = new StringBuilder();
                foreach (var item in networkShareFileNames)
                {
                    networkShareFileNamesOutput.AppendLine(item);
                }

                StringBuilder detailedJitMethodsOutput = new StringBuilder();
                int jitCount = 0;
                foreach (var item in detailedJitMethods.OrderByDescending(key => key.Value))
                {
                    jitCount++;
                    detailedJitMethodsOutput.AppendLine($"{item.Key} : {item.Value}");
                }

                StringBuilder dwasDetailedJitMethodsOutput = new StringBuilder();
                foreach (var item in dwasDetailedJitMethods.OrderByDescending(key => key.Value))
                {
                    dwasDetailedJitMethodsOutput.AppendLine($"{item.Key} : {item.Value}");
                }

                StringBuilder detailedDiskReadOutput = new StringBuilder();
                foreach (var item in diskReadFiles.OrderByDescending(key => key.Value))
                {
                    detailedDiskReadOutput.AppendLine($"{item.Key} : {item.Value}");
                }

                StringBuilder memoryHardFaultsOutput = new StringBuilder();
                foreach (var item in memoryHardFaults.OrderByDescending(key => key.Value.Item2))
                {
                    memoryHardFaultsOutput.AppendLine($"{item.Key} (count: {item.Value.Item1}) : {item.Value.Item2}");
                }

                StringBuilder dwasOutboundCallsOutput = new StringBuilder();
                foreach (var item in dwasOutboundCalls)
                {
                    dwasOutboundCallsOutput.AppendLine($"{item.Item1} : {item.Item2}");
                }

                StringBuilder languageWorkerMemoryHardFaultsOutput = new StringBuilder();
                StringBuilder languageWorkerDetailedJitMethodsOutput = new StringBuilder();
                StringBuilder languageWorkerDetailedAssemblyLoaderMethodsOutput = new StringBuilder();
                StringBuilder languageWorkerDetailedTypeLoadsOutput = new StringBuilder();
                int languageWorkerJitCount = 0;
                int languageWorkerAssemblyLoaderCount = 0;
                int languageWorkerTypeLoadCount = 0;
                if (languageWorkerPid != 0)
                {
                    foreach (var item in languageWorkerMemoryHardFaults.OrderByDescending(key => key.Value.Item2))
                    {
                        languageWorkerMemoryHardFaultsOutput.AppendLine($"{item.Key} (count: {item.Value.Item1}) : {item.Value.Item2}");
                    }

                    foreach (var item in languageWorkerDetailedJitMethods.OrderByDescending(key => key.Value))
                    {
                        languageWorkerJitCount++;
                        languageWorkerDetailedJitMethodsOutput.AppendLine($"{item.Key} : {item.Value}");
                    }

                    foreach (var item in languageWorkerDetailedAssemblyLoaderMethods.OrderByDescending(key => key.Value))
                    {
                        languageWorkerAssemblyLoaderCount++;
                        languageWorkerDetailedAssemblyLoaderMethodsOutput.AppendLine($"{item.Key} : {item.Value}");
                    }

                    foreach (var item in languageWorkerDetailedTypeLoads.OrderByDescending(key => key.Value))
                    {
                        languageWorkerTypeLoadCount++;
                        languageWorkerDetailedTypeLoadsOutput.AppendLine($"{item.Key} : {item.Value}");
                    }
                }

                output.AppendLine("");
                output.AppendLine($"--pid {functionsHostPid} --exclude-events-before {relativeStartTimeStamp} --exclude-events-after {relativeEndfTimeStamp}");
                output.AppendLine($"--app-name {appName} --activity-id {activityId} --host-version {functionsHostVersion}");

                output.AppendLine($"\nTotal cold start time msec: {coldStartMsec}");
                output.AppendLine($"HttpStatus: {httpStatus}");

                output.AppendLine($"\nTotal CPU time during cold start msec (2 cores): {totalCpuSamples}");
                output.AppendLine($"Functions WebHost CPU time during cold start msec: {functionsHostCpuTime}");
                output.AppendLine($"DWAS CPU time during cold start msec: {dwasCpuTime}");
                output.AppendLine($"Language Worker CPU time during cold start msec: {languageWorkerCpuTime}");

                output.AppendLine($"\nFunctions WebHost JIT time during specialization msec: {jitTime} (count:{jitCount})");
                output.AppendLine($"Functions WebHost GC time during specialization msec: {gcTime}");
                output.AppendLine($"DWAS GC time during specialization msec: {dwasGCTime}");
                output.AppendLine($"DWAS JIT time during specialization msec: {dwasJitTime}  (count:{dwasDetailedJitMethods.Count})");
                output.AppendLine($"Total Disk read time during specialization msec: {diskReadTime}");
                output.AppendLine($"Total WebHost Functions memory hard faults time during specialization msec: {memoryHardFaultTime}");
                output.AppendLine($"\nTotal DWAS provisioning time msec: {totalDwasProvisioningTime}");
                output.AppendLine($"Total DWAS outbound calls time during specialization msec: {totalDwasOutboundCallsTime}");
                output.AppendLine($"\nFunctions WebHost GC allocation during specialization in bytes: {string.Format("{0:n0}", gcAllocationInBytes)}");
                output.AppendLine($"DWAS GC allocation during specialization in bytes: {string.Format("{0:n0}", dwasGCAllocationInBytes)}");

                if (languageWorkerPid != 0)
                {
                    output.AppendLine($"\nLanguageWorkerPid: {languageWorkerPid}");
                    output.AppendLine($"Language Worker JIT time during specialization msec: {languageWorkerJitTime} (count:{languageWorkerJitCount})");
                    output.AppendLine($"Language Worker Assembly Loader time during specialization msec: {languageWorkerAssemblyLoaderTime} (count:{languageWorkerAssemblyLoaderCount})");
                    output.AppendLine($"Language Worker Type Load time during specialization msec: {languageWorkerTypeLoadTime} (count:{languageWorkerTypeLoadCount})");
                    output.AppendLine($"Language Worker GC time during specialization msec: {languageWorkerGCTime}");
                    output.AppendLine($"Total Language Worker memory hard faults time during specialization msec: {languageWorkerMemoryHardFaultTime}");
                }

                output.AppendLine("\nCPU Usage by active processes during cold start:\n");
                output.AppendLine(activeProcessesOutput.ToString());
                output.AppendLine("\nNetwork share accesses:\n");
                output.AppendLine(networkShareFileNamesOutput.ToString());
                output.AppendLine("\nDetailed JIT Times:\n");
                output.AppendLine(detailedJitMethodsOutput.ToString());
                output.AppendLine("\nDetailed DWAS JIT Times:\n");
                output.AppendLine(dwasDetailedJitMethodsOutput.ToString());
                output.AppendLine("\nDetailed Disk Reads:\n");
                output.AppendLine(detailedDiskReadOutput.ToString());
                output.AppendLine("\nDetailed Memory Hard Faults:\n");
                output.AppendLine(memoryHardFaultsOutput.ToString());
                output.AppendLine("\nDWAS outbound calls:\n");
                output.AppendLine(dwasOutboundCallsOutput.ToString());
                output.AppendLine("\nDWAS cold start perf data:\n");
                output.AppendLine(dwasColdStartPerfData.ToString());

                if (languageWorkerPid != 0)
                {
                    output.AppendLine("\nDetailed Language Worker Memory Hard Faults:\n");
                    output.AppendLine(languageWorkerMemoryHardFaultsOutput.ToString());
                    output.AppendLine("\nDetailed Language Worker JIT Times:\n");
                    output.AppendLine(languageWorkerDetailedJitMethodsOutput.ToString());
                    output.AppendLine("\nDetailed Language Worker Assembly Loader Times:\n");
                    output.AppendLine(languageWorkerDetailedAssemblyLoaderMethodsOutput.ToString());
                    output.AppendLine("\nDetailed Language Worker Type Load Times:\n");
                    output.AppendLine(languageWorkerDetailedTypeLoadsOutput.ToString());
                }

                Console.WriteLine(output);
                Console.WriteLine($"Writing output to: {outputFileName}");

                FunctionsColdStartAnalyzerEventSource.Instance.LogColdStartAnalysis(appName, activityId, functionsHostPid, (ulong)Math.Round(relativeStartTimeStamp), (ulong)Math.Round(relativeEndfTimeStamp), etlFileName,
                    ColdStartTime: (ulong)Math.Round(coldStartMsec), JitTime: (ulong)Math.Round(jitTime), FunctionsGCTime: (ulong)Math.Round(gcTime), DwasGCTime: (ulong)Math.Round(dwasGCTime), DiskReadTime: (ulong)Math.Round(diskReadTime),
                    ActiveProcesses: activeProcessesOutput.ToString().WithMaxLength(), NetworkShareAccesses: networkShareFileNamesOutput.ToString().WithMaxLength(), DetailedJIT: detailedJitMethodsOutput.ToString().WithMaxLength(), DetailedDiskRead: detailedDiskReadOutput.ToString().WithMaxLength(), FunctionsHostVersion: functionsHostVersion,
                    GCAllocationInBytes: (ulong)gcAllocationInBytes, DwasGCAllocationInBytes: (ulong)dwasGCAllocationInBytes,
                    FunctionsMemoryHardFaultTime: (ulong)Math.Round(memoryHardFaultTime), FunctionsDetailedMemoryHardFaults: memoryHardFaultsOutput.ToString().WithMaxLength(),
                    TotalDwasOutboundCallsTime: totalDwasOutboundCallsTime, DwasOutboundCalls: dwasOutboundCallsOutput.ToString().WithMaxLength(), TotalDwasProvisioningTime: totalDwasProvisioningTime, DwasColdStartPerfData: dwasColdStartPerfData,
                    HttpStatus: httpStatus, DwasJitTime: (ulong)Math.Round(dwasJitTime), DwasDetailedJIT: dwasDetailedJitMethodsOutput.ToString().WithMaxLength(),
                    LanguageWorkerJitTime: (ulong)Math.Round(languageWorkerJitTime), LanguageWorkerAssemblyLoaderTime: (ulong)Math.Round(languageWorkerAssemblyLoaderTime), LanguageWorkerGCTime: (ulong)Math.Round(languageWorkerGCTime), LanguageWorkerMemoryHardFaultTime: (ulong)Math.Round(languageWorkerMemoryHardFaultTime),
                    LanguageWorkerDetailedJIT: languageWorkerDetailedJitMethodsOutput.ToString().WithMaxLength(), LanguageWorkerDetailedAssemblyLoader: languageWorkerDetailedAssemblyLoaderMethodsOutput.ToString().WithMaxLength(), LanguageWorkerMemoryHardFaults: languageWorkerMemoryHardFaultsOutput.ToString().WithMaxLength(),
                    LanguageWorkerTypeLoadTime: (ulong)Math.Round(languageWorkerTypeLoadTime), LanguageWorkerDetailedTypeLoad: languageWorkerDetailedTypeLoadsOutput.ToString().WithMaxLength(),
                    JitCount: (ulong)jitCount, DwasJitCount: (ulong)dwasDetailedJitMethods.Count, LanguageWorkerJitCount: (ulong)languageWorkerJitCount, LanguageWorkerAssemblyLoaderCount: (ulong)languageWorkerAssemblyLoaderCount, LanguageWorkerTypeLoadCount: (ulong)languageWorkerTypeLoadCount,
                    totalCpuTime: (double)totalCpuSamples, functionsHostCpuTime: (double)functionsHostCpuTime, languageWorkerCpuTime: (double)languageWorkerCpuTime, dwasCpuTime: (double)dwasCpuTime
                    );

                if (!string.IsNullOrEmpty(outputFileName))
                {
                    try
                    {
                        File.WriteAllText(outputFileName, output.ToString());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to write to output file {outputFileName}: {ex.Message}");
                    }
                }

                CopyFilesForPgoIfNeeded(output.ToString(), etlFileName);

                return 0;
            }
        }

        private void CopyFilesForPgoIfNeeded(string coldStartContent, string etlFileName)
        {
            // This is for PGO only, app name must start with functiondev-pgo
            if (appName.StartsWith("functiondev-pgo-", StringComparison.OrdinalIgnoreCase) && Directory.Exists(pgoColdStartFileLocation))
            {
                // This is just a safety measure to make sure we are not piling .etl files on disk
                DeleteOldFilesFromPgoLocation();

                string newEtlFilePath = Path.Combine(pgoColdStartFileLocation, appName + "_" + Path.GetFileName(etlFileName));
                try
                {
                    File.Copy(etlFileName, newEtlFilePath, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to copy ETL file from {etlFileName} to {newEtlFilePath} \n exception: {ex.Message}");
                }

                string coldStartFilePath = Path.Combine(pgoColdStartFileLocation, appName + "_" + Path.GetFileName(etlFileName) + ".coldstart");
                try
                {
                    File.WriteAllText(coldStartFilePath, coldStartContent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write to output file {coldStartFilePath}: {ex.Message}");
                }
            }
        }

        private void DeleteOldFilesFromPgoLocation()
        {
            DirectoryInfo directory = new DirectoryInfo(pgoColdStartFileLocation);

            // If there are more than 10 files, then delete the oldest ones.
            var etlFiles = directory.GetFiles("*.etl").OrderBy(n => n.CreationTimeUtc);
            DeleteOldestFiles(etlFiles, 10);

            var coldStartlFiles = directory.GetFiles("*.coldstart").OrderBy(n => n.CreationTimeUtc);
            DeleteOldestFiles(coldStartlFiles, 10);
        }

        private static void DeleteOldestFiles(IOrderedEnumerable<FileInfo> files, int number)
        {
            if (files.Count() > number)
            {
                var filesToDelete = files.Take(files.Count() - number);

                foreach (var file in filesToDelete)
                {
                    try
                    {
                        File.Delete(file.FullName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete file {file.FullName}: {ex.Message}");
                    }
                }
            }
        }

        private void ProcessEvents(string urlPattern, TraceLog traceLog)
        {
            Guid contextId = Guid.Empty;

            foreach (var eventData in traceLog.Events)
            {
                try
                {
                    if (eventData.ProviderName == "IIS_Trace" && eventData.EventName == "IISGeneral/GENERAL_REQUEST_START" && eventData.PayloadByName("AppPoolId").ToString() == "OnDemandConfigAndForwarder")
                    {
                        var requestUrl = (string)eventData.PayloadByName("RequestURL");

                        // if urlPattern is not provided, then search for sla-ws-func/functiondev apps and /api/ in RequestURL
                        if ((urlPattern != string.Empty && requestUrl.Contains(urlPattern, StringComparison.OrdinalIgnoreCase)) ||
                            (urlPattern == string.Empty &&
                            (requestUrl.StartsWith("http://sla-ws-func", StringComparison.OrdinalIgnoreCase) || requestUrl.StartsWith("http://functiondev", StringComparison.OrdinalIgnoreCase)) &&
                            requestUrl.Contains("/api/", StringComparison.OrdinalIgnoreCase)))
                        {
                            // This means there are multiple SLA cold starts in the profile which are not interesting. We care about the last cold start in the profile
                            // Anything before last one will be ignored.
                            if (relativeStartTimeStamp != double.MaxValue)
                            {
                                ClearAll();
                            }

                            relativeStartTimeStamp = Math.Min(relativeStartTimeStamp, eventData.TimeStampRelativeMSec);
                            contextId = (Guid)eventData.PayloadByName("ContextId");
                        }
                    }
                    // This is only used for running placeholder mode locally.
                    if (eventData.ProviderName == "Microsoft-Diagnostics-DiagnosticSource" && eventData.EventName == "Activity1Start/Start" && eventData.ProcessName.Contains("Microsoft.Azure.WebJobs.Script.WebHost", StringComparison.OrdinalIgnoreCase))
                    {
                        dynamic args = eventData.PayloadByName("Arguments");
                        if(args != null && args.Length >= 2)
                        {
                            dynamic kvp = args[2];
                            foreach (var item in kvp)
                            {
                                if (item.Value.ToString().Contains("/api/", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (relativeStartTimeStamp != double.MaxValue)
                                    {
                                        ClearAll();
                                    }
                                    relativeStartTimeStamp = Math.Min(relativeStartTimeStamp, eventData.TimeStampRelativeMSec);
                                    functionsHostPid = eventData.ProcessID;
                                }
                            }
                        }
                    }
                    if (eventData.TimeStampRelativeMSec > relativeStartTimeStamp)
                    {
                        // The 2nd IIS_Trace/IISGeneral/GENERAL_REQUEST_START is for the functions process and we can use that to find the PID of the function process. The first one is for miniARR.
                        if (functionsHostPid == 0 && eventData.ProviderName == "IIS_Trace" && eventData.EventName == "IISGeneral/GENERAL_REQUEST_START")
                        {
                            var requestUrl = (string)eventData.PayloadByName("RequestURL");

                            // if urlPattern is not provided, then search for sla-ws-func/functiondev apps and /api/ in RequestURL
                            if ((urlPattern != string.Empty && requestUrl.Contains(urlPattern, StringComparison.OrdinalIgnoreCase)) ||
                                (urlPattern == string.Empty &&
                                (requestUrl.StartsWith("http://sla-ws-func", StringComparison.OrdinalIgnoreCase) || requestUrl.StartsWith("http://functiondev", StringComparison.OrdinalIgnoreCase)) &&
                                requestUrl.Contains("/api/", StringComparison.OrdinalIgnoreCase)))
                            {

                                functionsHostPid = eventData.ProcessID;
                            }
                        }
                    }

                    // For below events, we want to make sure we only report them for duration of cold start
                    if (eventData.TimeStampRelativeMSec > relativeStartTimeStamp && eventData.TimeStampRelativeMSec <= relativeEndfTimeStamp)
                    {
                        if (functionsHostPid != 0)
                        {
                            jitTime = CalculateJitTimes(eventData, functionsHostPid, jitTime, individualJitMethods, detailedJitMethods);
                            gcTime = CalculateGCTimes(eventData, functionsHostPid, gcTime, gcCounts);
                            ReportNetworkShareAccesses(eventData);
                            GetAppDetails(eventData);
                            memoryHardFaultTime = CalculateMemoryHardFaults(eventData, functionsHostPid, memoryHardFaultTime, memoryHardFaults);
                            GetLanguageWorkerDetails(eventData);
                        }

                        CalculateDiskReadTimes(eventData);

                        if (dwasPid != 0)
                        {
                            dwasGCTime = CalculateGCTimes(eventData, dwasPid, dwasGCTime, dawsGCCounts);
                            dwasJitTime = CalculateJitTimes(eventData, dwasPid, dwasJitTime, dwasIndividualJitMethods, dwasDetailedJitMethods);
                        }
                        else
                        {
                            GetDwasPid(eventData);
                        }

                        CalculateGCAllocationTimes(eventData);
                        CalculateCpuUsageByProcesses(eventData);
                        ReportDwasColdStartDetails(eventData);
                    }

                    if (eventData.ProviderName == "IIS_Trace" && eventData.EventName == "IISGeneral/GENERAL_REQUEST_END" &&
                        Guid.Equals((Guid)eventData.PayloadByName("ContextId"), contextId))
                    {
                        relativeEndfTimeStamp = eventData.TimeStampRelativeMSec;
                        int.TryParse(eventData.PayloadByName("HttpStatus").ToString(), out httpStatus);
                    }
                    // This is only used for running placeholder mode locally.
                    if (eventData.ProviderName == "Microsoft-Diagnostics-DiagnosticSource" && eventData.EventName == "Activity1Stop/Stop"
                        && eventData.ProcessName.Contains("Microsoft.Azure.WebJobs.Script.WebHost", StringComparison.OrdinalIgnoreCase))
                    {
                        relativeEndfTimeStamp = eventData.TimeStampRelativeMSec;
                        httpStatus = 200;
                    }

                    if (functionsHostPid != 0 && eventData.ProviderName == "Windows Kernel" && eventData.EventName == "Process/DCStop")
                    {
                        // These events only happen during rundown and we need to capture them for active process report. One event per process
                        var processDetail = eventData.ProcessName + "(" + eventData.ProcessID + ")";
                        var commandLine = (string)eventData.PayloadByName("CommandLine");

                        processInfo.Add(processDetail, commandLine.Length > 100 ? commandLine.Substring(0, 100) : commandLine);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to process events  : {ex.Message}");
                }
            }
        }

        private void GetLanguageWorkerDetails(TraceEvent eventData)
        {
            if (languageWorkerPid == 0)
            {
                GetLanguageWorkerPid(eventData);
            }

            if (eventData.ProcessID == languageWorkerPid)
            {
                languageWorkerMemoryHardFaultTime = CalculateMemoryHardFaults(eventData, languageWorkerPid, languageWorkerMemoryHardFaultTime, languageWorkerMemoryHardFaults);
                languageWorkerJitTime = CalculateJitTimes(eventData, languageWorkerPid, languageWorkerJitTime, languageWorkerIndividualJitMethods, languageWorkerDetailedJitMethods);
                languageWorkerGCTime = CalculateGCTimes(eventData, languageWorkerPid, languageWorkerGCTime, languageWorkerGCCounts);
                languageWorkerAssemblyLoaderTime = CalculateAssemblyLoadTimes(eventData, languageWorkerPid, languageWorkerAssemblyLoaderTime, languageWorkerIndividualAssemblyLoaderMethods, languageWorkerDetailedAssemblyLoaderMethods);
                languageWorkerTypeLoadTime = CalculateTypeLoadTimes(eventData, languageWorkerPid, languageWorkerTypeLoadTime, languageWorkerIndividualTypeLoads, languageWorkerDetailedTypeLoads);
            }
        }

        private void ReportDwasColdStartDetails(TraceEvent eventData)
        {
            if (eventData.ProcessName == "DWASSVC" && eventData.ProviderName == "Microsoft-Windows-WebSites")
            {
                try
                {
                    if (eventData.EventName == "EventID(65401)")
                    {
                        var requestUrl = eventData.PayloadByName("RequestUrl").ToString();
                        var latency = (long)eventData.PayloadByName("LatencyInMilliseconds");

                        dwasOutboundCalls.Add(new Tuple<string, long>(requestUrl, latency));
                        totalDwasOutboundCallsTime += latency;
                    }
                    else if (eventData.EventName == "EventID(15005)")
                    {
                        totalDwasProvisioningTime = (int)eventData.PayloadByName("TotalTimeTakenForProvisioning");
                        dwasColdStartPerfData = eventData.PayloadByName("ColdStartPerfData").ToString();
                    }
                }
                catch (Exception ex)
                {
                    // Ignore the exception and continue. This may happen if DWAS changes event names or types when reporting this.
                }
            }
        }

        private double CalculateMemoryHardFaults(TraceEvent eventData, int pid, double memoryHardFaultTime, Dictionary<string, Tuple<int, double>> memoryHardFaults)
        {
            // Only look at HardFault events for the process we are interested in
            if (eventData.ProcessID == pid && eventData.ProviderName == "Windows Kernel" && eventData.EventName == "Memory/HardFault")
            {
                var fileName = (string)eventData.PayloadByName("FileName");
                double currentElapsedTime = (double)eventData.PayloadByName("ElapsedTimeMSec");

                memoryHardFaultTime += currentElapsedTime;

                if (memoryHardFaults.ContainsKey(fileName))
                {
                    int count = (memoryHardFaults[fileName].Item1) + 1;
                    double elapsedTime = (memoryHardFaults[fileName].Item2) + currentElapsedTime;
                    memoryHardFaults[fileName] = new Tuple<int, double>(count, elapsedTime);
                }
                else
                {
                    memoryHardFaults.Add(fileName, new Tuple<int, double>(1, currentElapsedTime));
                }
            }
            return memoryHardFaultTime;
        }

        private int GetPidAlternateMethod(string urlPattern, TraceLog traceLog)
        {
            foreach (var eventData in traceLog.Events)
            {
                if (eventData.TimeStampRelativeMSec > relativeStartTimeStamp && eventData.ProviderName == "Microsoft-Diagnostics-DiagnosticSource" && eventData.EventName == "Activity1Start/Start" && (string)eventData.PayloadByName("EventName") == "Microsoft.AspNetCore.Hosting.BeginRequest")
                {
                    foreach (var arguments in (IEnumerable)eventData.PayloadByName("Arguments"))
                    {
                        // if urlPattern is not provided, then search for  /api/ in RequestURL
                        if ((urlPattern != string.Empty && arguments.ToString().Contains(urlPattern, StringComparison.OrdinalIgnoreCase)) ||
                            urlPattern == string.Empty && arguments.ToString().Contains("/api/", StringComparison.OrdinalIgnoreCase))
                        {
                            return eventData.ProcessID;
                        }
                    }
                }
            }
            return 0;
        }

        private void ClearAll()
        {
            appName = string.Empty;
            activityId = string.Empty;
            functionsHostVersion = string.Empty;

            individualJitMethods.Clear();
            dwasIndividualJitMethods.Clear();
            languageWorkerIndividualJitMethods.Clear();

            detailedJitMethods.Clear();
            dwasDetailedJitMethods.Clear();
            languageWorkerDetailedJitMethods.Clear();

            diskReadFiles.Clear();
            dwasOutboundCalls.Clear();
            gcCounts.Clear();
            dawsGCCounts.Clear();
            languageWorkerGCCounts.Clear();
            activeProcesses.Clear();
            processInfo.Clear();
            networkShareFileNames.Clear();
            memoryHardFaults.Clear();
            languageWorkerMemoryHardFaults.Clear();

            languageWorkerIndividualAssemblyLoaderMethods.Clear();
            languageWorkerDetailedAssemblyLoaderMethods.Clear();
            languageWorkerIndividualTypeLoads.Clear();
            languageWorkerDetailedTypeLoads.Clear();

            dwasColdStartPerfData = string.Empty;

            httpStatus = 0;
            functionsHostPid = 0;
            dwasPid = 0;
            languageWorkerPid = 0;
            jitTime = 0;
            dwasJitTime = 0;
            languageWorkerJitTime = 0;
            gcTime = 0;
            dwasGCTime = 0;
            languageWorkerGCTime = 0;
            diskReadTime = 0;
            totalCpuSamples = 0;
            memoryHardFaultTime = 0;
            languageWorkerMemoryHardFaultTime = 0;
            languageWorkerAssemblyLoaderTime = 0;
            languageWorkerTypeLoadTime = 0;
            totalDwasOutboundCallsTime = 0;
            totalDwasProvisioningTime = 0;

            gcAllocationInBytes = 0;
            dwasGCAllocationInBytes = 0;

            functionsHostCpuTime = 0;
            languageWorkerCpuTime = 0;
            dwasCpuTime = 0;

            relativeStartTimeStamp = double.MaxValue;
            relativeEndfTimeStamp = double.MaxValue;
        }

        private void ReportNetworkShareAccesses(TraceEvent eventData)
        {
            // check network access for functions process as well as language workers and DWAS.
            if ((eventData.ProcessID == functionsHostPid || eventData.ProcessID == languageWorkerPid || eventData.ProcessID == dwasPid)
                && eventData.ProviderName == "Microsoft-Windows-Kernel-File" && eventData.EventName == "Create")
            {
                // This is to report all file access across network shares by functions process during cold start in case there are regressions.
                var fileName = eventData.PayloadByName("FileName").ToString();
                if (fileName.StartsWith("\\\\"))
                {
                    networkShareFileNames.Add(fileName);
                }
            }
        }

        private void CalculateCpuUsageByProcesses(TraceEvent eventData)
        {
            if (eventData.ProviderName == "Windows Kernel" && eventData.EventName == "PerfInfo/Sample")
            {
                totalCpuSamples++;

                var processDetail = new Tuple<string, int>(eventData.ProcessName, eventData.ProcessID);

                if (activeProcesses.ContainsKey(processDetail))
                {
                    activeProcesses[processDetail] += 1;
                }
                else
                {
                    activeProcesses.Add(processDetail, 1);
                }
            }
        }

        private void CalculateDiskReadTimes(TraceEvent eventData)
        {
            if (eventData.ProviderName == "Windows Kernel" && eventData.EventName == "DiskIO/Read")
            {
                var fileName = (string)eventData.PayloadByName("FileName");
                double currentDiskServiceTime = (double)eventData.PayloadByName("DiskServiceTimeMSec");

                // TODO: should we exclude specific paths?
                if (diskReadFiles.ContainsKey(fileName))
                {
                    diskReadFiles[fileName] += currentDiskServiceTime;
                }
                else
                {
                    diskReadFiles.Add(fileName, currentDiskServiceTime);
                }

                diskReadTime += currentDiskServiceTime;
            }
        }

        private double CalculateGCTimes(TraceEvent eventData, int pid, double gcTime, Dictionary<int, double> gcCounts)
        {
            // Only look at GC events for the process we are interested in
            if (eventData.ProcessID == pid && eventData.ProviderName == "Microsoft-Windows-DotNETRuntime")
            {
                if (eventData.EventName == "GC/Start")
                {
                    gcCounts.Add((int)eventData.PayloadByName("Count"), eventData.TimeStampRelativeMSec);
                }

                if (eventData.EventName == "GC/Stop")
                {
                    var countId = (int)eventData.PayloadByName("Count");
                    if (gcCounts.ContainsKey(countId))
                    {
                        gcTime += eventData.TimeStampRelativeMSec - (gcCounts[countId]);
                    }
                }
            }
            return gcTime;
        }

        private double CalculateJitTimes(TraceEvent eventData, int pid, double jitTime, Dictionary<string, double> individualJitMethods, Dictionary<string, double> detailedJitMethods)
        {
            // Only look at JIT events for the process we are interested in
            if (eventData.ProcessID == pid && eventData.ProviderName == "Microsoft-Windows-DotNETRuntime")
            {
                if (eventData.EventName == "Method/JittingStarted")
                {
                    // In rare cases, it is possible some methods are jitted multiple times, we will only report the last one
                    if(individualJitMethods.ContainsKey(eventData.PayloadByName("MethodID").ToString()))
                    {
                        individualJitMethods[eventData.PayloadByName("MethodID").ToString()] = eventData.TimeStampRelativeMSec;
                    }
                    else
                    {
                        individualJitMethods.Add(eventData.PayloadByName("MethodID").ToString(), eventData.TimeStampRelativeMSec);
                    }
                }

                if (eventData.EventName == "Method/LoadVerbose")
                {
                    var methodId = eventData.PayloadByName("MethodID").ToString();
                    var methodName = eventData.PayloadByName("MethodNamespace").ToString() + "::" + eventData.PayloadByName("MethodName").ToString();
                    double currentJitTime;

                    if (individualJitMethods.ContainsKey(methodId))
                    {
                        currentJitTime = eventData.TimeStampRelativeMSec - (individualJitMethods[methodId]);
                        jitTime += currentJitTime;

                        if (detailedJitMethods.ContainsKey(methodName))
                        {
                            detailedJitMethods[methodName] += currentJitTime;
                        }
                        else
                        {
                            detailedJitMethods.Add(methodName, currentJitTime);
                        }
                    }
                }
            }
            return jitTime;
        }

        private double CalculateAssemblyLoadTimes(TraceEvent eventData, int pid, double assemblyLoaderTime, Dictionary<string, double> individualAssemblyLoaderMethods, Dictionary<string, double> detailedAssemblyLoaderMethods)
        {
            if (eventData.ProcessID == pid && eventData.ProviderName == "Microsoft-Windows-DotNETRuntime")
            {
                var assemblyInfo = "";
                if (eventData.EventName == "AssemblyLoader/Start")
                {
                    assemblyInfo = GetAssemblyInfo(eventData, assemblyInfo);

                    if (!individualAssemblyLoaderMethods.ContainsKey(assemblyInfo))
                    {
                        individualAssemblyLoaderMethods.Add(assemblyInfo, eventData.TimeStampRelativeMSec);
                    }
                    else
                    {
                        individualAssemblyLoaderMethods[assemblyInfo] = eventData.TimeStampRelativeMSec;
                    }
                }

                if (eventData.EventName == "AssemblyLoader/Stop")
                {
                    assemblyInfo = GetAssemblyInfo(eventData, assemblyInfo);
                    double currentAssemblyLoaderTime;

                    if (individualAssemblyLoaderMethods.ContainsKey(assemblyInfo))
                    {
                        currentAssemblyLoaderTime = eventData.TimeStampRelativeMSec - (individualAssemblyLoaderMethods[assemblyInfo]);
                        assemblyLoaderTime += currentAssemblyLoaderTime;

                        if (detailedAssemblyLoaderMethods.ContainsKey(assemblyInfo))
                        {
                            detailedAssemblyLoaderMethods[assemblyInfo] += currentAssemblyLoaderTime;
                        }
                        else
                        {
                            detailedAssemblyLoaderMethods.Add(assemblyInfo, currentAssemblyLoaderTime);
                        }
                    }
                }
            }
            return assemblyLoaderTime;
        }

        private double CalculateTypeLoadTimes(TraceEvent eventData, int pid, double typeLoadTime, Dictionary<string, double> individualTypeLoads, Dictionary<string, double> detailedTypeLoads)
        {
            if (eventData.ProcessID == pid && eventData.ProviderName == "Microsoft-Windows-DotNETRuntime")
            {
                string typeLoadStartId;
                if (eventData.EventName == "TypeLoad/Start")
                {
                    typeLoadStartId = eventData.PayloadByName("TypeLoadStartID")?.ToString() ?? "";

                    if (!string.IsNullOrEmpty(typeLoadStartId))
                    {
                        if (!individualTypeLoads.ContainsKey(typeLoadStartId))
                        {
                            individualTypeLoads.Add(typeLoadStartId, eventData.TimeStampRelativeMSec);
                        }
                        else
                        {
                            individualTypeLoads[typeLoadStartId] = eventData.TimeStampRelativeMSec;
                        }
                    }
                }

                if (eventData.EventName == "TypeLoad/Stop")
                {
                    typeLoadStartId = eventData.PayloadByName("TypeLoadStartID")?.ToString() ?? "";
                    double currentTypeLoadTime;

                    if (!string.IsNullOrEmpty(typeLoadStartId) && individualTypeLoads.ContainsKey(typeLoadStartId))
                    {
                        currentTypeLoadTime = eventData.TimeStampRelativeMSec - (individualTypeLoads[typeLoadStartId]);
                        typeLoadTime += currentTypeLoadTime;

                        var typeName = eventData.PayloadByName("TypeName")?.ToString() ?? "";

                        if (!string.IsNullOrEmpty(typeName))
                        {
                            if (detailedTypeLoads.ContainsKey(typeName))
                            {
                                detailedTypeLoads[typeName] += currentTypeLoadTime;
                            }
                            else
                            {
                                detailedTypeLoads.Add(typeName, currentTypeLoadTime);
                            }
                        }
                    }
                }
            }
            return typeLoadTime;
        }

        private static string GetAssemblyInfo(TraceEvent eventData, string assemblyInfo)
        {
            if (!string.IsNullOrEmpty(eventData.PayloadByName("AssemblyName").ToString()))
            {
                assemblyInfo = eventData.PayloadByName("AssemblyName").ToString();
            }
            else if (!string.IsNullOrEmpty(eventData.PayloadByName("AssemblyPath").ToString()))
            {
                assemblyInfo = eventData.PayloadByName("AssemblyPath").ToString();
            }

            return assemblyInfo;
        }

        private void CalculateGCAllocationTimes(TraceEvent eventData)
        {
            if (eventData.ProviderName == "Microsoft-Windows-DotNETRuntime" && eventData.EventName == "GC/AllocationTick")
            {
                if (eventData.ProcessName == "DWASSVC")
                {
                    dwasGCAllocationInBytes += (int)eventData.PayloadByName("AllocationAmount");
                }
                if (eventData.ProcessID == functionsHostPid)
                {
                    gcAllocationInBytes += (int)eventData.PayloadByName("AllocationAmount");
                }
            }
        }

        private void GetAppDetails(TraceEvent eventData)
        {
            if (eventData.ProcessID == functionsHostPid && eventData.ProviderName == "FunctionsSystemLogsEventSource" && eventData.EventName == "RaiseFunctionsEventInfo")
            {
                // TODO: Revisit this when we onboard Linux, there are betetr ways to get this info but this works for both Windows and Linux.
                if (eventData.PayloadByName("EventName").ToString() == "ExecutedHttpRequest")
                {
                    appName = eventData.PayloadByName("AppName")?.ToString();
                    var summary = eventData.PayloadByName("Summary")?.ToString();

                    var beg = summary.IndexOf("requestId:");
                    if (beg >= 0)
                    {
                        var end = summary.IndexOf(",", beg);
                        activityId = summary?.Substring(beg, end - beg).Replace("requestId: \"", "");
                    }
                    else
                    {
                        beg = summary.IndexOf("requestId\":");
                        if (beg >= 0)
                        {
                            var end = summary.IndexOf("\",", beg);
                            activityId = summary?.Substring(beg, end - beg).Replace("requestId\":", "").Replace("\"", "").Trim();
                        }
                    }
                    functionsHostVersion = eventData.PayloadByName("HostVersion")?.ToString();
                }
            }
        }

        private void GetLanguageWorkerPid(TraceEvent eventData)
        {
            if (eventData.ProcessID == functionsHostPid && eventData.ProviderName == "FunctionsSystemLogsEventSource" && eventData.EventName == "RaiseFunctionsEventVerbose")
            {
                var summary = eventData.PayloadByName("Summary")?.ToString();

                // This is logged in Functions Host Codebase - https://github.com/Azure/azure-functions-host/blob/dev/src%2FWebJobs.Script.Grpc%2FChannel%2FGrpcWorkerChannel.cs#L362 
                var searchString = "Sending FunctionEnvironmentReloadRequest to WorkerProcess with Pid: ";
                if (summary.Contains(searchString))
                {
                    int.TryParse(summary.Replace(searchString, "").Trim('\''), out languageWorkerPid);
                }
                else if(summary.Contains("process with Id=") && summary.EndsWith("started"))
                {
                    // This is logged in Functions Host codebase - https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Workers/ProcessManagement/WorkerProcess.cs#L75
                    var match = languageWorkerPidMatch.Match(summary);
                    if (match.Success)
                    {
                        int.TryParse(match.Groups[1].Value, out languageWorkerPid);
                    }
                }
            }
            if (languageWorkerPid == 0 && eventData.ProcessName.Contains("FunctionsNetHost", StringComparison.OrdinalIgnoreCase) && eventData.ProviderName == "Microsoft-Windows-DotNETRuntime" && eventData.EventName == "Runtime/Start")
            {
                languageWorkerPid = eventData.ProcessID;
            }
        }

        private void GetDwasPid(TraceEvent eventData)
        {
            // There is only one DWAS process running on Wndows workers.
            if (dwasPid == 0 && eventData.ProcessName == "DWASSVC")
            {
                dwasPid= eventData.ProcessID;
            }
        }

    }
}
