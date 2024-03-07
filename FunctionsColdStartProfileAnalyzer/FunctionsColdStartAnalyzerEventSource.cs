using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FunctionsColdStartProfileAnalyzer
{
    [EventSource(Guid="D7C2E7EF-6429-46BF-93C2-D259A3B2475B")]
    class FunctionsColdStartAnalyzerEventSource : EventSource
    {
        internal static readonly FunctionsColdStartAnalyzerEventSource Instance = new FunctionsColdStartAnalyzerEventSource();

        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", Justification = "MDS columns names are Pascal Cased")]
        [Event(1000, Level = EventLevel.Informational, Channel = EventChannel.Operational, Version = 6)]
        public void LogColdStartAnalysis(string AppName, string ActivityId, int ProcessId, ulong ExcludeEventsBefore, ulong ExcludeEventsAfter, string ProfileFileName, ulong ColdStartTime, ulong JitTime, ulong FunctionsGCTime, ulong DwasGCTime, ulong DiskReadTime,
            string ActiveProcesses, string NetworkShareAccesses, string DetailedJIT, string DetailedDiskRead, string FunctionsHostVersion, ulong GCAllocationInBytes, ulong DwasGCAllocationInBytes,
            ulong FunctionsMemoryHardFaultTime, string FunctionsDetailedMemoryHardFaults,
            long TotalDwasOutboundCallsTime, string DwasOutboundCalls, int TotalDwasProvisioningTime, string DwasColdStartPerfData,
            int HttpStatus, ulong DwasJitTime, string DwasDetailedJIT,
            ulong LanguageWorkerJitTime, ulong LanguageWorkerAssemblyLoaderTime, ulong LanguageWorkerGCTime, ulong LanguageWorkerMemoryHardFaultTime,
            string LanguageWorkerDetailedJIT, string LanguageWorkerDetailedAssemblyLoader, string LanguageWorkerMemoryHardFaults,
            ulong LanguageWorkerTypeLoadTime, string LanguageWorkerDetailedTypeLoad,
            ulong JitCount, ulong DwasJitCount, ulong LanguageWorkerJitCount, ulong LanguageWorkerAssemblyLoaderCount, ulong LanguageWorkerTypeLoadCount,
            double totalCpuTime, double functionsHostCpuTime, double languageWorkerCpuTime, double dwasCpuTime
            )
        {
            if (IsEnabled())
            {
                WriteEvent(1000, AppName, ActivityId, ProcessId, ExcludeEventsBefore, ExcludeEventsAfter, ProfileFileName, ColdStartTime, JitTime, FunctionsGCTime, DwasGCTime, DiskReadTime, 
                    ActiveProcesses, NetworkShareAccesses, DetailedJIT, DetailedDiskRead, FunctionsHostVersion, GCAllocationInBytes, DwasGCAllocationInBytes,
                    FunctionsMemoryHardFaultTime, FunctionsDetailedMemoryHardFaults,
                    TotalDwasOutboundCallsTime, DwasOutboundCalls, TotalDwasProvisioningTime, DwasColdStartPerfData,
                    HttpStatus, DwasJitTime, DwasDetailedJIT,
                    LanguageWorkerJitTime, LanguageWorkerAssemblyLoaderTime, LanguageWorkerGCTime, LanguageWorkerMemoryHardFaultTime,
                    LanguageWorkerDetailedJIT, LanguageWorkerDetailedAssemblyLoader, LanguageWorkerMemoryHardFaults,
                    LanguageWorkerTypeLoadTime, LanguageWorkerDetailedTypeLoad,
                    JitCount, DwasJitCount, LanguageWorkerJitCount, LanguageWorkerAssemblyLoaderCount, LanguageWorkerTypeLoadCount,
                    totalCpuTime, functionsHostCpuTime, languageWorkerCpuTime, dwasCpuTime
                    );
            }
        }

        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", Justification = "MDS columns names are Pascal Cased")]
        [Event(1001, Level = EventLevel.Warning, Channel = EventChannel.Operational)]
        public void LogIISEventNotFound(string ProfileFileName, string Message)
        {
            if(IsEnabled())
            {
                WriteEvent(1001, ProfileFileName, Message);
            }
        }
    }
}
