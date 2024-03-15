#Requires -RunAsAdministrator

[CmdletBinding(PositionalBinding=$false)]
Param(
    # Run App Specific Parameters

    [string]$appHostDir = (Get-Location),
    [string]$perfviewExePath = (Join-Path $appHostDir "PerfView.exe"),

    [ValidateSet("base", "prejit", "preload", "preload+prejit")] `
      [string]$scenario = "base",

    # Profile Trace Specific Parameters

    [string]$analyzerName = "FunctionsColdStartProfileAnalyzer.exe",
    [string]$analyzerPath = (Get-Location),
    [string[]]$options = @(),

    # Universal Parameters

    [ValidateSet("run", "analyze", "all")][string]$mode = "all",
    [string]$traceName = "PerfViewData.etl",
    [string]$tracePath = $appHostDir
)

# *********************
# Auxiliary Functions!
# *********************

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
    Write-Host "`n$(""*"" * 50)"
    Write-Host "STARTING $StageName STAGE"
    Write-Host "$(""*"" * 50)`n"
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

    Write-Host "Setting cwd to '$analyzerPath'..."
    Set-Location -Path $analyzerPath

    # *******************************************************
    # Here is where we rely on the C# app to do the parsing.
    # *******************************************************

    # $analyzerArgs = @($traceFullPath)

    # Write-Host "`nRunning $analyzerExePath $($analyzerArgs -Join ' ')"
    # Start-Process -FilePath $analyzerExePath `
    #               -ArgumentList $analyzerArgs `
    #               -NoNewWindow -Wait

    # Restore the original path of the terminal from which this script was run.

    Write-Host "Restoring cwd to '$originalDir'..."
    Set-Location -Path $originalDir
}

# *****************
# Main App Script!
# *****************

Write-Host "`nLaunching script...`n"

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

if (($mode -ieq "run") -or ($mode -ieq "all")) { Run-App }
if (($mode -ieq "analyze") -or ($mode -ieq "all")) { Analyze-App }

Write-Host "`nScript finished!`n"
