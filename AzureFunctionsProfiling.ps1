#Requires -RunAsAdministrator

[CmdletBinding(PositionalBinding=$false)]
Param(
    # Run App Specific Parameters

    [switch][Alias('h')]$help,
    [string][Alias('apphost')]$appHostDir = (Get-Location),
    [string][Alias('perfview')]$perfviewExePath = (Join-Path $appHostDir "PerfView.exe"),

    [ValidateSet("base", "prejit", "preload", "preload+prejit")] `
      [string]$scenario = "base",

    # Profile Trace Specific Parameters

    [string]$analyzerName = "FunctionsColdStartProfileAnalyzer.exe",
    [string]$analyzerPath = (Get-Location),
    [ValidateSet("jit-time","detailed-jit","condensed-jit")][string[]]$options = @(),

    # Universal Parameters

    [ValidateSet("run", "analyze", "all")][string]$mode = "all",
    [string]$traceName = "PerfViewData.etl",
    [string]$tracePath = $appHostDir
)

# *********************
# Auxiliary Functions!
# *********************

# /// Display-Help()
# This little function prints a brief message about the script, and a small
# explanation on what each flag does.

function Display-Help()
{
    Write-Host "`nUsage: AzureFunctionsProfiling.ps1 <parameters go here>`n" `
      -ForegroundColor "DarkCyan"

    Write-Host "-h, -help:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Display this message.`n"

    Write-Host "-apphost, -appHostDir:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Path to the directory containing everything to test (i.e. The" `
               "FunctionApp44 folder Azure Functions gave us) [Default: This folder]`n"

    Write-Host "-perfview, -perfviewExePath:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Path to the 'perfview.exe' that will be used to capture the profiles" `
               "and traces. [Default: This folder + perfview.exe]`n"

    Write-Host "-scenario:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Scenario you wish to run 'func-harness' on. Possible values are" `
               "base, prejit, preload, and preload+prejit. [Default: base]`n"

    Write-Host "-analyzerName:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Name of the analyzer tool (i.e. FunctionsColdStartProfileAnalyzer.exe" `
               "that they gave us). This can be omitted if passed as part of the" `
               "full path given to `"-analyzerPath.`"" `
               "[Default: FunctionsColdStartProfileAnalyzer.exe]`n"

    Write-Host "-analyzerPath:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Path where the analyzer tool is located. May include the name of" `
               "the exe file if `"-analyzerName`" is omitted. [Default: This folder]`n"

    Write-Host "-mode:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Mode of running this script:"
    Write-Host "  * Run: Only run the 'func-harness' app and capture the trace."
    Write-Host "  * Analyze: Only run the analyzer tool on the provided trace."
    Write-Host "  * All: Do both. Run 'func-harness', and then run the analyzer" `
               "tool on the resulting trace."
    Write-Host "[Default: all]`n"

    Write-Host "-traceName:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Name of the trace to be captured and/or profiled. This can be" `
               "omitted if passed as part of the full path given to `"-tracePath`"." `
               "[Default: PerfviewData.etl]`n"

    Write-Host "-tracePath:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Path where the trace will be saved to and/or read from. May include" `
               "the name of the .etl/.etlx file if `"-traceName`" is omitted." `
               "[Default: App Host Path]`n"
}

# /// Validate-RunPaths()
# This function performs the necessary checks to ensure all paths are valid, so
# we can begin the run safely.

function Validate-RunPaths()
{
    if (-not (Test-Path -Path $appHostDir))
    {
        Write-Host "The given work path '$appHostDir' was unfortunately not found :(`n"
        return $false
    }

    if (-not (Test-Path -Path $perfviewExePath))
    {
        Write-Host "The given PerfView path '$perfviewExePath' was unfortunately not" `
                   "found :(`n"
        return $false
    }

    # Not finding the given traces path is not the end of the world. We can save
    # the traces to the default working directory in this case.
    if (-not (Test-Path -Path $tracePath))
    {
        Write-Host "The given traces path '$tracePath' was unfortunately not found :("
        Write-Host "Setting it to the working path '$appHostDir'..."
        $tracePath = $appHostDir
    }

    return $true
}

# /// Print-Banner
# This function just prints a nice-looking banner to show the user which stage
# this little script is currently in.

function Print-Banner([string]$StageName)
{
    $banner = "STARTING $StageName STAGE"
    Write-Host "`n$(""*"" * $banner.Length)"
    Write-Host $banner
    Write-Host "$(""*"" * $banner.Length)`n"
}

# ****************
# Main Functions!
# ****************

# /// Run-App()
# This function runs the Functions App Host with dotnet's tool 'func-harness'.

function Run-App()
{
    # If we get erroneous paths, then we can't do any work, so we exit.
    if (-Not (Validate-RunPaths)) { exit 1 }

    # Display a nice banner indicating we're working on the 'Run' stage :)
    Print-Banner -StageName "RUNNING"

    # We save the place where this script was called from to keep the user's environment
    # consistent and undisrupted.
    $originalDir = (Get-Location)
    $traceFullPath = (Join-Path $tracePath $traceName)

    Write-Host "Setting cwd to '$appHostDir'..."
    Set-Location -Path $appHostDir

    # JSON settings filenames we will require.
    $runnerSettingsFile   = "harness.settings.json"
    $scenarioSettingsFile = "harness.settings_$scenario.json"
    $backupSettingsFile   = "harness.settings_backup.json"

    # With full path.
    $runnerSettingsPath   = (Join-Path $appHostDir $runnerSettingsFile)
    $scenarioSettingsPath = (Join-Path $appHostDir $scenarioSettingsFile)
    $backupSettingsPath   = (Join-Path $appHostDir $backupSettingsFile)

    # Check if there is already a 'harness.settings.json' file. If yes, then we need
    # to rename it to make space for the scenario we want to run. This is because the
    # 'func-harness' tool was written to always search for a file called 'harness.settings.json'.

    if (Test-Path -Path $runnerSettingsPath)
    {
        Write-Host "Renaming '$runnerSettingsFile' to '$backupSettingsFile'..."
        Rename-Item -Path $runnerSettingsPath -NewName $backupSettingsFile
    }

    Write-Host "Renaming '$scenarioSettingsFile' to '$runnerSettingsFile'..."
    Rename-Item -Path $scenarioSettingsPath -NewName $runnerSettingsFile

    # Azure Functions gave us some specific providers to capture the right
    # profiling data.
    $providers = @(
        "DCCCCC7B-F393-4852-96AE-BB6769A266C4",
        "E30BA2D3-75B8-4E96-9F82-F41EAAC243E5",
        "69CB2C45-CAF6-48C1-81F9-4C59D93CF43B"
    )

    $perfviewCaptureArgs = @(
        "/DataFile:""$traceFullPath""",
        "/BufferSizeMB:256",
        "/LogFile:$traceFullPath.log",
        "/StackCompression",
        "/CircularMB:500",
        "/Providers:""$($providers -Join ',')""",
        "/NoGui",
        "/NoNGenRundown",
        "/Merge:True",
        "/Zip:False",
        "run",
        "func-harness"
    )

    Write-Host "`nRunning $perfviewExePath $($perfviewCaptureArgs -Join ' ')"
    Start-Process -FilePath $perfviewExePath `
                  -ArgumentList $perfviewCaptureArgs `
                  -NoNewWindow -Wait

    # Once everything's done, restore the folder/files structure to how it was.

    Write-Host "`nRestoring '$runnerSettingsFile' to '$scenarioSettingsFile'..."
    Rename-Item -Path $runnerSettingsPath -NewName $scenarioSettingsFile

    Write-Host "Restoring '$backupSettingsFile' to '$runnerSettingsFile'..."
    Rename-Item -Path $backupSettingsPath -NewName $runnerSettingsFile

    # Restore the original path of the terminal from which this script was run.

    Write-Host "Restoring cwd to '$originalDir'..."
    Set-Location -Path $originalDir
}

# /// Analyze-App()
# This function runs the Azure Functions profiler app to analyze the given trace.

function Analyze-App()
{
    $analyzerExePath = (Join-Path $analyzerPath $analyzerName)
    $traceFullPath = (Join-Path $tracePath $traceName)

    if (-not (Test-Path -Path $analyzerExePath))
    {
        Write-Host "`nThe given profile analyzer tool '$analyzerFullPath' was" `
                   "unfortunately not found :(`n"
        return $false
    }

    if (-not (Test-Path -Path $traceFullPath))
    {
        Write-Host "`nThe given trace file '$traceFullPath' was unfortunately" `
                   "not found :(`n"
        return $false
    }

    # Display a nice banner indicating we're working on the 'Analyze' stage :)
    Print-Banner -StageName "ANALYZING"

    # We save the place where this script was called from to keep the user's environment
    # consistent and undisrupted.
    $originalDir = (Get-Location)
    $appAnalyzerDir = (Join-Path $PSScriptRoot "AppAnalyzer")
    $appAnalyzerExePath = (Join-Path $appAnalyzerDir "out")

    Write-Host "Setting cwd to '$appAnalyzerDir'..."
    Set-Location -Path $appAnalyzerDir

    # *******************************************************
    # Here is where we rely on the C# app to do the parsing.
    # *******************************************************

    # We should also check if it has been built, and build it if not.

    if (-not (Test-Path -Path $appAnalyzerExePath))
    {
        Write-Host ""
        Start-Process -FilePath "dotnet" `
                      -ArgumentList @("build", "-c", "Release", "-o", "out") `
                      -NoNewWindow -Wait
        Write-Host "`nFinished building the dotnet app!"
    }

    # Write-Host "Here we will call the AppAnalyzer C# app to help us."
    # Write-Host "Under Construction! Coming Soon!"

    Start-Process -FilePath (Join-Path $appAnalyzerExePath "AppAnalyzer.exe") `
                  -ArgumentList (@($analyzerExePath, $traceFullPath) + $options) `
                  -NoNewWindow -Wait

    # Restore the original path of the terminal from which this script was run.

    Write-Host "`nRestoring cwd to '$originalDir'..."
    Set-Location -Path $originalDir
}

# *****************
# Main App Script!
# *****************

Write-Host "`nLaunching script..."

if ($help)
{
    Display-Help
    exit 0
}

# I want to give my team the liberty of passing the trace path as either the
# folder containing the traces, or the trace file itself, using the '-tracePath'
# command-line argument. However, here in the script, we do need to work with
# both, path and trace, separately at some points. So, since we also support the
# '-traceName' flag, we need to work some magic to make this transparent to
# the user.

if (([System.IO.Path]::GetExtension($tracePath) -eq ".etl") `
    -or ([System.IO.Path]::GetExtension($tracePath) -eq ".etlx"))
{
    $traceName = (Split-Path -Path $tracePath -Leaf)
    $tracePath = (Split-Path -Path $tracePath -Parent)
}

if ([System.IO.Path]::GetExtension($analyzerPath) -eq ".exe")
{
    $analyzerName = (Split-Path -Path $analyzerPath -Leaf)
    $analyzerPath = (Split-Path -Path $analyzerPath -Parent)
}

if (($mode -ieq "run") -or ($mode -ieq "all")) { Run-App }
if (($mode -ieq "analyze") -or ($mode -ieq "all")) { Analyze-App }

Write-Host "`nScript finished!`n"
