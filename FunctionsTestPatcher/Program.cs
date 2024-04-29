// File: Program.cs

using System;

namespace AzureFunctions;

public class Program
{
    static int Main(string[] args)
    {
        MainContext context = CommandLineParser.ParseToContext(args);

        Utils.PrintBanner("AZURE FUNCTIONS!");
        Stage_BuildPatchFunctionsNetHost(context);

        return 0;
    }

    private static void Stage_BuildPatchFunctionsNetHost(MainContext ctx)
    {
        var azureManager = new FunctionsNetHostManager {
            ProfilingArtifactsPath = "C:\\Development\\AzureFunctionsStuff\\ArtifactsForProfileCollection",
            RepoPath = "C:\\Development\\AzureFunctionsStuff\\Azure_azure-functions-dotnet-worker",
            SDKPath = "C:\\Development\\AzureFunctionsStuff\\TestOurs\\dotnet-sdk-nightly--preview3"
        };

        azureManager.PrepareHostAndArtifacts();
    }
}
