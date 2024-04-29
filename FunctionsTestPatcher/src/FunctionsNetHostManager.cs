// File: FunctionsNetHostManager.cs

using System;
using System.IO;

using static System.OperatingSystem;

internal class FunctionsNetHostManager
{
    public string ProfilingArtifactsPath { get; init; }
    public string RepoPath { get; init; }
    public string SDKPath { get; init; }

    private readonly string _exeExt;
    private readonly string _dotnetExe;

    private readonly string _buildPath;
    private readonly string _publishPath;

    public FunctionsNetHostManager()
    {
        _exeExt = IsWindows() ? ".exe" : string.Empty;
        _dotnetExe = Path.Combine(SDKPath, $"dotnet{_exeExt}");

        // TODO: Update with user parameters rather than hard-coded values.
        _buildPath = Path.Combine("bin", "Release", "net9.0");
        _publishPath = Path.Combine("bin", "Release", "net9.0", "win-x64", "publish");
    }

    public void PrepareHostAndArtifacts(string workPath = "")
    {
        if (string.IsNullOrEmpty(workPath))
            workPath = Path.GetDirectoryName(ProfilingArtifactsPath);

        BuildRepo();
        PatchArtifactsForProfileCollection(workPath);
    }

    public void PatchArtifactsForProfileCollection(string workPath)
    {
        // Steps:
        // 1) Make a copy of the ArtifactsForProfileCollection.
        // 2) Copy the Function44 bin artifacts to FunctionApp44Base.
        // 3) Copy the FunctionsNetHostBase bin exe and nethost to FunctionsNetHostBase.
        // 4) Make a PlaceholderApp and StartupHook folders in FunctionsNetHostBase.
        // 5) Copy the two dummy apps' bin artifacts to their newly created folders.

        string profileTestingPath = Path.Combine(workPath, "Test");
        string srcSamplesPath = Path.Combine(RepoPath, "samples");

        // TODO: Clean up these long lines of code.
        string functionsHostBins = Path.Combine(RepoPath, "host", "src", "FunctionsNetHost", _publishPath);
        string functionApp44Bins = Path.Combine(srcSamplesPath, "FunctionApp44", _buildPath);
        string dummyAppsBins = Path.Combine(srcSamplesPath, "PlaceholderApp", _buildPath);

        Utils.PrintBanner("Setting up profiling artifacts!");

        Console.Write("\nDeleting existing artifacts folder {profileTestingPath}...");
        Directory.Delete(profileTestingPath, true);

        Console.Write("\nCreating new artifacts folder {profileTestingPath} and"
                      + " subfolders...\n");

        Directory.CreateDirectory(profileTestingPath);

        Directory.CreateDirectory(Path.Combine(profileTestingPath,
                                               "FunctionApp44Base"));

        Directory.CreateDirectory(Path.Combine(profileTestingPath,
                                               "FunctionsNetHostBase"));

        Directory.CreateDirectory(Path.Combine(profileTestingPath,
                                               "FunctionsNetHostBase",
                                               "PlaceholderApp"));

        Directory.CreateDirectory(Path.Combine(profileTestingPath,
                                               "FunctionsNetHostBase",
                                               "StartupHook"));

        string[] netHostBins = { "FunctionsNetHost.exe", "nethost.dll" };
        Console.Write("\nCopying FunctionsNetHost binaries...\n");

        foreach (string bin in netHostBins)
        {
            string publishedBinPath = Path.Combine(functionsHostBins, bin);
            string targetPath = Path.Combine(profileTestingPath, "FunctionsNetHostBase", bin);
            Console.Write($"\nCopying {bin} to {targetPath}...");
            File.Copy(publishedBinPath, targetPath);
        }

        Console.Write("\nCopying Placeholder App binaries...\n");

        foreach (string binPath in Directory.EnumerateFiles(dummyAppsBins))
        {
            string filename = Path.GetFileName(binPath);

            if (filename.StartsWith("PlaceholderApp"))
            {
                string targetPath = Path.Combine(profileTestingPath,
                                                 "FunctionsNetHostBase",
                                                 "PlaceholderApp",
                                                 filename);

                Console.Write($"\nCopying {filename} to {targetPath}...");
                File.Copy(binPath, targetPath);
            }
            else if (filename.StartsWith("StartupHook"))
            {
                string targetPath = Path.Combine(profileTestingPath,
                                                 "FunctionsNetHostBase",
                                                 "StartupHook",
                                                 filename);

                Console.Write($"\nCopying {filename} to {targetPath}...");
                File.Copy(binPath, targetPath);
            }
        }

        Console.Write("\nCopying FunctionApp44 binaries...\n");

        foreach (string binPath in Directory.EnumerateFiles(functionApp44Bins))
        {
            string filename = Path.GetFileName(binPath);
            string targetPath = Path.Combine(profileTestingPath,
                                             "FunctionApp44Base",
                                             filename);

            Console.Write($"\nCopying {filename} to {targetPath}...");
            File.Copy(binPath, targetPath);
        }

        Utils.System("pwsh", $"Copy-Item -Path {functionApp44Bins}\\.azurefunctions -Destination {profileTestingPath}\\FunctionApp44Base\\.azurefunctions");
        Console.Write("\n");
        Console.Write("\nFinished copying all binaries!\n");
    }

    public void BuildRepo()
    {
        // if (string.IsNullOrEmpty(_exeExt))
        //     _exeExt = IsWindows() ? ".exe" : string.Empty;

        // if (string.IsNullOrEmpty(_dotnetExe))
        //     _dotnetExe = Path.Combine(SDKPath, $"dotnet{_exeExt}");

        BuildDevPack();
        BuildFunctionsNetHost();
        BuildPlaceholderApps();
    }

    private void BuildDevPack()
    {
        string devpackPS = Path.Combine(RepoPath, "tools", "devpack.ps1");
        string devpackArgs = $"-PatchedDotnet \"{_dotnetExe}\"";
        string pwshArgs = $"-NoProfile {devpackPS} {devpackArgs}";

        Utils.PrintBanner("Building Functions DevPack!");
        Console.Write($"\npwsh {pwshArgs}");
        Utils.System("pwsh", pwshArgs);
    }

    private void BuildFunctionsNetHost()
    {
        // TODO: Handle the platform universally.
        string hostSrc = Path.Combine(RepoPath, "host", "src");
        string publishArgs = "publish -c Release -r win-x64";

        Utils.PrintBanner("Building and publishing Functions NetHost!");
        Console.Write($"\n{_dotnetExe} {publishArgs}");
        Utils.System(_dotnetExe, publishArgs, hostSrc);
    }

    private void BuildPlaceholderApps()
    {
        string samplesPath = Path.Combine(RepoPath, "samples");

        string[] csprojFiles = {
            Path.Combine(samplesPath, "FunctionApp44", "FunctionApp44.csproj"),
            Path.Combine(samplesPath, "PlaceholderApp", "PlaceholderApp.csproj"),
            Path.Combine(samplesPath, "PlaceholderApp", "StartupHook.csproj")
        };

        Utils.PrintBanner("Building sample apps!");

        foreach (string proj in csprojFiles)
        {
            string buildArgs = $"build {proj} -c Release";
            Console.Write($"\nBuilding {Path.GetFileName(proj)}...\n");
            Console.Write($"\n{_dotnetExe} {buildArgs}\n");
            Utils.System(_dotnetExe, buildArgs);
        }
    }
}
