using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

using CultureInfo = System.Globalization.CultureInfo;

public class SDKPatcher
{
    private sealed class Context
    {
        public string Arch       { get; init; }
        public string Config     { get; init; }
        public string NetVersion { get; init; }
        public string OS         { get; init; }
        public string RepoRoot   { get; init; }
        public string WorkPath   { get; init; }

        public bool Redownload { get; init; }

        public string Platform         { get { return $"{OS}-{Arch}"; } }
        public string RepoPlatform     { get { return $"{OS}.{Arch}.{Config}"; } }
        public string RepoAltPlatform  { get { return $"{Platform}.{Config}"; } }
        public string ArtifactsBinPath { get { return Path.Join(RepoRoot, "artifacts", "bin"); } }

        public Context()
        {
            // Platform = $"{OS}-{Arch}";
            // RepoPlatform = $"{OS}.{Arch}.{Config}";
            // RepoAltPlatform = $"{Platform}.{Config}";
            // ArtifactsBinPath = Path.Join(RepoRoot, "artifacts", "bin");
        }
    }

    private const string _sdkRootFolder = "dotnet-sdk-nightly";

    // args[0]: Architecture
    // args[1]: Configuration
    // args[2]: Operating System
    // args[3]: Runtime Repo Path
    // args[4]: Working Path
    // args[5]: Redownload SDK?

    static async Task Main(string[] args)
    {
        Console.WriteLine("\nLaunching Script...!\n");

        if (args.Length < 6)
        {
            throw new ArgumentException("Not enough arguments. Will update this"
                                        + " error message later.\n");
        }

        Context ctx = new Context
        {
            Arch = args[0].ToLower(),
            Config = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(args[1].ToLower()),
            NetVersion = "net9.0",
            OS = args[2].ToLower(),
            RepoRoot = args[3],
            WorkPath = args[4],
            Redownload = args[5] == "true" ? true : false,
        };

        await DownloadExtractNightlySDK(ctx);
        PatchSDK(ctx);
    }

    static void PatchSDK(Context ctx)
    {
        DirectoryInfo sdkRootDI = new DirectoryInfo(Path.Join(ctx.WorkPath,
                                                              _sdkRootFolder));

        // Now, we need to get all the target paths within the SDK where we will
        // copy stuff to, which some contain very specific version numbers like
        // 'preview-4.24204.3'. So, we need to get that string from one of the
        // inner folder names. Any folder in the SDK that contains the full version
        // name is fine. We're using the one in host/fxr here because it's the only
        // one, and therefore we don't need to do
        // any further filtering or processing.

        // I'm annoyed so let me get away with this long methods chain pls :)
        string sdkVersion = new DirectoryInfo(
                                Path.Join(sdkRootDI.FullName,
                                          "host",
                                          "fxr"))
                                .GetDirectories("9.0.*")[0]
                                .Name;

        Dictionary<string, string> assemblyPaths = new Dictionary<string, string>
        {
            { "Repo_Coreclr",
              Path.Join(ctx.ArtifactsBinPath, "coreclr", ctx.RepoPlatform) },

            { "Repo_Corehost",
              Path.Join(ctx.ArtifactsBinPath, ctx.RepoAltPlatform, "corehost") },

            { "Repo_NetcoreAppRef",
              Path.Join(ctx.ArtifactsBinPath,
                        "microsoft.netcore.app.ref",
                        "ref",
                        ctx.NetVersion) },

            { "Repo_NetcoreAppRuntime",
              Path.Join(ctx.ArtifactsBinPath,
                        $"microsoft.netcore.app.runtime.{ctx.Platform}",
                        ctx.Config,
                        "runtimes",
                        ctx.Platform,
                        "lib",
                        ctx.NetVersion) },

            { "SDK_HostFxr",
              Path.Join(sdkRootDI.FullName, "host", "fxr", sdkVersion) },

            { "SDK_NativePacks",
              Path.Join(sdkRootDI.FullName,
                        "packs",
                        $"Microsoft.NETCore.App.Host.{ctx.Platform}",
                        sdkVersion,
                        "runtimes",
                        ctx.Platform,
                        "native") },

            { "SDK_Framework",
              Path.Join(sdkRootDI.FullName, "shared", "Microsoft.NETCore.App", sdkVersion) },

            { "SDK_RefPacks",
              Path.Join(sdkRootDI.FullName,
                        "packs",
                        "Microsoft.NETCore.App.Ref",
                        sdkVersion,
                        "ref",
                        ctx.NetVersion) }
        };

        string copyFrom = string.Empty;
        string copyTo = string.Empty;

        // Patching HostFxr

        copyFrom = Path.Join(assemblyPaths["Repo_Corehost"], "libhostfxr.so");
        copyTo = Path.Join(assemblyPaths["SDK_HostFxr"], "libhostfxr.so");
        PatchFile(copyFrom, copyTo);

        // Patching Native Packs

        copyFrom = Path.Join(assemblyPaths["Repo_Corehost"], "apphost");
        copyTo = Path.Join(assemblyPaths["SDK_NativePacks"], "apphost");
        PatchFile(copyFrom, copyTo);

        copyFrom = Path.Join(assemblyPaths["Repo_Corehost"], "coreclr_delegates.h");
        copyTo = Path.Join(assemblyPaths["SDK_NativePacks"], "coreclr_delegates.h");
        PatchFile(copyFrom, copyTo);

        copyFrom = Path.Join(assemblyPaths["Repo_Corehost"], "hostfxr.h");
        copyTo = Path.Join(assemblyPaths["SDK_NativePacks"], "hostfxr.h");
        PatchFile(copyFrom, copyTo);

        copyFrom = Path.Join(assemblyPaths["Repo_Corehost"], "libnethost.a");
        copyTo = Path.Join(assemblyPaths["SDK_NativePacks"], "libnethost.a");
        PatchFile(copyFrom, copyTo);

        copyFrom = Path.Join(assemblyPaths["Repo_Corehost"], "libnethost.so");
        copyTo = Path.Join(assemblyPaths["SDK_NativePacks"], "libnethost.so");
        PatchFile(copyFrom, copyTo);

        copyFrom = Path.Join(assemblyPaths["Repo_Corehost"], "nethost.h");
        copyTo = Path.Join(assemblyPaths["SDK_NativePacks"], "nethost.h");
        PatchFile(copyFrom, copyTo);

        copyFrom = Path.Join(assemblyPaths["Repo_Corehost"], "singlefilehost");
        copyTo = Path.Join(assemblyPaths["SDK_NativePacks"], "singlefilehost");
        PatchFile(copyFrom, copyTo);

        // Patching Framework

        copyFrom = Path.Join(assemblyPaths["Repo_Corehost"], "libhostpolicy.so");
        copyTo = Path.Join(assemblyPaths["SDK_Framework"], "libhostpolicy.so");
        PatchFile(copyFrom, copyTo);

        copyFrom = Path.Join(assemblyPaths["Repo_Coreclr"], "libcoreclr.so");
        copyTo = Path.Join(assemblyPaths["SDK_Framework"], "libcoreclr.so");
        PatchFile(copyFrom, copyTo);

        copyFrom = Path.Join(assemblyPaths["Repo_Coreclr"], "System.Private.CoreLib.dll");
        copyTo = Path.Join(assemblyPaths["SDK_Framework"], "System.Private.CoreLib.dll");
        PatchFile(copyFrom, copyTo);

        copyFrom = Path.Join(assemblyPaths["Repo_NetcoreAppRuntime"], "System.Runtime.dll");
        copyTo = Path.Join(assemblyPaths["SDK_Framework"], "System.Runtime.dll");
        PatchFile(copyFrom, copyTo);

        // Patching Native Refs

        copyFrom = Path.Join(assemblyPaths["Repo_NetcoreAppRef"], "System.Runtime.dll");
        copyTo = Path.Join(assemblyPaths["SDK_RefPacks"], "System.Runtime.dll");
        PatchFile(copyFrom, copyTo);

        Console.WriteLine("\nFinished patching the SDK!\n");
    }

    static void PatchFile(string source, string target)
    {
        Console.WriteLine($"Patching {target}...");
        Console.WriteLine($"From {source}...\n");
        File.Copy(source, target, overwrite: true);
    }

    static async Task DownloadExtractNightlySDK(Context ctx)
    {
        string zipExt = ctx.OS.Equals("windows") ? "zip" : "tar.gz";
        string downloadZip = Path.Join(ctx.WorkPath, $"dotnet-sdk-nightly.{zipExt}");
        string extractFolder = Path.Join(ctx.WorkPath, _sdkRootFolder);

        string nightlyURL = "https://aka.ms/dotnet/9.0.1xx/daily/"
                            + $"dotnet-sdk-{ctx.Platform}.{zipExt}";

        // Wanting to download a new SDK nightly build means we want to start an
        // experiment from a clean slate. So, first we delete any remains from
        // previous experiments.

        if (Directory.Exists(extractFolder))
        {
            Console.WriteLine($"Cleaning up {extractFolder}...");
            Directory.Delete(extractFolder, recursive: true);
        }

        // Zipped/Compressed downloaded SDK archives are clean, so if we find one
        // here, there is no need to download it again, unless explicitly required
        // with the Redownload flag. For some reason, Azure servers have been really
        // slow in responding to and serving download requests, so we'll take any
        // opportunity to optimize our testing procedures.

        if (!File.Exists(downloadZip) && ctx.Redownload)
        {
            Console.WriteLine("Redownload flag found so cleaning up existing"
                              + $" {downloadZip}...");
            File.Delete(downloadZip);
        }

        if (!File.Exists(downloadZip))
            await DownloadFile(nightlyURL, downloadZip);
        else
            Console.WriteLine($"Found {downloadZip}...");

        ExtractCompressedFile(downloadZip, extractFolder);
    }

    static async Task DownloadFile(string url, string pathToSave)
    {
        Console.WriteLine($"Downloading {url}...");
        try
        {
            using HttpClient webClient = new HttpClient();
            using Stream dlStream = await webClient.GetStreamAsync(url);
            using FileStream saveStream = File.Create(pathToSave);
            dlStream.CopyTo(saveStream);
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Apologies, but it seems that the dotnetbuilds.azureedge.net"
                              + " servers are lagging or outright not working again."
                              + " You might have to try downloading the nightly SDK"
                              + " directly from your browser. If you do, don't forget"
                              + " to rename it to what this script expects before"
                              + " trying the patching process again :)");

            string expectedZipName = new DirectoryInfo(pathToSave).Name;
            Console.WriteLine("\nExpected zip name: {expectedZipName}\n");
            Environment.Exit(-1);
        }
    }

    static void ExtractCompressedFile(string file, string targetDest)
    {
        string extension = Path.GetExtension(file);

        if (!Directory.Exists(targetDest))
        {
            Console.WriteLine($"Creating extraction destination {targetDest}...");
            _ = Directory.CreateDirectory(targetDest);
        }

        // C# has native support for extracting a variety of compression formats.
        // However, the functionality is implemented in/with different classes,
        // depending on the format, hence we need to treat them differently.

        if (extension.Equals(".zip"))
        {
            // Zip files are the most straightforward because the ZipFile class
            // takes care of everything.

            Console.WriteLine($"Extracting {file} using ZipFile...\n");
            ZipFile.ExtractToDirectory(file, targetDest, overwriteFiles: true);
        }
        else
        {
            // Tar.gz files are a bit more complex to work with and decompress.
            // C# in theory supports extracting tar's and gz's out of the box,
            // but it's all over the place and sometimes it works, other times
            // it doesn't. So, to make things easier, we'll just use the system's
            // installed 'tar' utility.

            Console.WriteLine($"Extracting {file} using the system's tar utility...\n");

            using (Process decompressor = new Process())
            {
                ProcessStartInfo dpsi = new ProcessStartInfo
                {
                    ArgumentList = { "-zxf", file, "-C", targetDest },
                    CreateNoWindow = true,
                    FileName = "tar",
                    UseShellExecute = false
                };

                decompressor.StartInfo = dpsi;
                decompressor.Start();
                decompressor.WaitForExit();
            }
        }
    }
}
