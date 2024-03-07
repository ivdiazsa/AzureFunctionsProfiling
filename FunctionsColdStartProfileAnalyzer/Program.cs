using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace FunctionsColdStartProfileAnalyzer
{
    class Program
    {
        internal static void UnZipIfNecessary(ref string inputFileName, TextWriter log)
        {
            if (inputFileName.EndsWith(".trace.zip", StringComparison.OrdinalIgnoreCase))
            {
                log.WriteLine($"'{inputFileName}' is a linux trace.");
                return;
            }

            var extension = Path.GetExtension(inputFileName);
            if (string.Compare(extension, ".zip", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(extension, ".vspx", StringComparison.OrdinalIgnoreCase) == 0)
            {
                string unzipedEtlFile;
                if (inputFileName.EndsWith(".etl.zip", StringComparison.OrdinalIgnoreCase))
                {
                    unzipedEtlFile = inputFileName.Substring(0, inputFileName.Length - 4);
                }
                else if (inputFileName.EndsWith(".vspx", StringComparison.OrdinalIgnoreCase))
                {
                    unzipedEtlFile = Path.ChangeExtension(inputFileName, ".etl");
                }
                else
                {
                    throw new ApplicationException("File does not end with the .etl.zip file extension");
                }

                ZippedETLReader etlReader = new ZippedETLReader(inputFileName, log);
                etlReader.EtlFileName = unzipedEtlFile;

                // Figure out where to put the symbols.  
                var inputDir = Path.GetDirectoryName(inputFileName);
                if (inputDir.Length == 0)
                {
                    inputDir = ".";
                }

                var symbolsDir = Path.Combine(inputDir, "symbols");
                etlReader.SymbolDirectory = symbolsDir;
                if (!Directory.Exists(symbolsDir))
                    Directory.CreateDirectory(symbolsDir);
                log.WriteLine("Putting symbols in {0}", etlReader.SymbolDirectory);

                etlReader.UnpackArchive();
                inputFileName = unzipedEtlFile;
            }
        }

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                ShowHelp();
                return -1;
            }
            string traceFileName = args[0];

            string urlPattern = string.Empty;
            if (args.Length < 2)
            {
                Console.WriteLine("urlpattern is missing, will look for SLA sites with /api/ urls");
            }
            else
            {
                urlPattern = args[1];
            }

            string outputFileName = string.Empty;

            if (args.Length < 3)
            {
                var traceFilePath = Path.GetDirectoryName(traceFileName);
                var traceFileNameWithoutPath = Path.GetFileName(traceFileName);
                if (traceFileName.Contains("Profile"))
                {
                    if (traceFileNameWithoutPath.IndexOf("Profile") > 0)
                    {
                        outputFileName = Path.Combine(traceFilePath, traceFileNameWithoutPath.Substring(0, traceFileNameWithoutPath.IndexOf("Profile") - 1)) + ".coldstart";
                    }
                    else
                    {
                        outputFileName = Path.Combine(traceFilePath, traceFileNameWithoutPath + ".coldstart");
                    }
                }
                else
                {
                    outputFileName = Path.Combine(traceFilePath, traceFileNameWithoutPath + ".coldstart");
                }
            }
            else
            {
                outputFileName = args[2];
            }

            string etlFileName = traceFileName;
            foreach (string nettraceExtension in new string[] { ".netperf", ".netperf.zip", ".nettrace" })
            {
                if (traceFileName.EndsWith(nettraceExtension))
                {
                    etlFileName = Path.ChangeExtension(traceFileName, ".etlx");
                    Console.WriteLine($"Creating ETLX file {etlFileName} from {traceFileName}");
                    TraceLog.CreateFromEventPipeDataFile(traceFileName, etlFileName);
                }
            }

            string lttngExtension = ".trace.zip";
            if (traceFileName.EndsWith(lttngExtension))
            {
                etlFileName = Path.ChangeExtension(traceFileName, ".etlx");
                Console.WriteLine($"Creating ETLX file {etlFileName} from {traceFileName}");
                TraceLog.CreateFromLttngTextDataFile(traceFileName, etlFileName);
            }

            UnZipIfNecessary(ref etlFileName, Console.Out);

            var traceAnalyzer = new TraceAnalyzer();
            return traceAnalyzer.Analyze(outputFileName, urlPattern, etlFileName);
        }

        private static void ShowHelp()
        {
            Console.WriteLine("FunctionsColdStartProfileAnalyzer <trace file name> <urlpattern> <outputfilewithpid>");
            Console.WriteLine("Will read the <trace file name>, looking for a request for <urlpattern> and dump cold start info into <outputfilewithpid>");
        }
    }
}
