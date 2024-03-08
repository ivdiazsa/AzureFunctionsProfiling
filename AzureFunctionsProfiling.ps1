[CmdletBinding(PositionalBinding=$false)]
Param(
    [ValidateSet("run", "analyze", "all")][string]$modeToRun = "all",

    [string]$workDir = (Get-Location),
    [string]$perfviewExe = (Join-Path $workDir "PerfView.exe"),
    [string]$traceName = "PerfViewData.etl",
    [string]$tracePath = $workDir,

    [ValidateSet("base", "prejit", "preload", "preload+prejit")] `
      [string]$scenario = "base"
)

# *********************
# Auxiliary Functions!
# *********************

# This function performs the necessary checks to ensure all paths are valid, so
# we can begin the run safely.

function Validate-Paths()
{
    if (-Not (Test-Path -Path $workDir))
    {
        Write-Host "The given work path '$workDir' was unfortunately not found :(`n"
        return $false
    }

    if (-Not (Test-Path -Path $perfviewExe))
    {
        Write-Host "The given PerfView path '$perfviewExe' was unfortunately not found :(`n"
        return $false
    }

    # if (-Not (Test-Path -Path $scenarioSettingsPath))
    # {
    #     Write-Host "The given settings path '$scenarioSettingsPath' was unfortunately not found :(`n"
    #     return $false
    # }

    # Not finding the given traces path is not the end of the world. We can save
    # the traces to the default working directory in this case.
    if (-Not (Test-Path -Path $tracePath))
    {
        Write-Host "The given traces path '$tracePath' was unfortunately not found :("
        Write-Host "Setting it to the working path '$workDir'..."
        $tracePath = $workDir
    }

    return $true
}

# ****************
# Main Functions!
# ****************

# This function runs the Functions App Host with dotnet's tool 'func-harness'.

function Run-App()
{
    # If we get erroneous paths, then we can't do any work, so we exit.
    if (-Not (Validate-Paths)) { exit 1 }

    # We save the place where this script was called from to keep the user's environment
    # consistent and undisrupted.
    $originalDir = (Get-Location)

    Write-Host "Setting cwd to '$workDir'..."
    Set-Location -Path $workDir

    # JSON settings filenames we will require.
    $runnerSettingsFile   = "harness.settings.json"
    $scenarioSettingsFile = "harness.settings_$scenario.json"
    $backupSettingsFile   = "harness.settings_backup.json"

    # With full path.
    $runnerSettingsPath   = (Join-Path $workDir $runnerSettingsFile)
    $scenarioSettingsPath = (Join-Path $workDir $scenarioSettingsFile)
    $backupSettingsPath   = (Join-Path $workDir $backupSettingsFile)

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

    # Azure Functions gave us some specific providers to capture the right profiling data.
    $providers = @(
        "DCCCCC7B-F393-4852-96AE-BB6769A266C4",
        "E30BA2D3-75B8-4E96-9F82-F41EAAC243E5",
        "69CB2C45-CAF6-48C1-81F9-4C59D93CF43B"
    )

    $perfviewCaptureArgs = @(
        "/DataFile:""$(Join-Path $tracePath $traceName)""",
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

    Write-Host "`nRunning $perfviewExe $($perfviewCaptureArgs -Join ' ')"
    Start-Process -FilePath $perfviewExe `
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

# This function runs the Azure Functions profiler app to analyze the given trace.

function Profile-App()
{
    Write-Host "Under Construction!"
}

# *****************
# Main App Script!
# *****************

Write-Host "`nLaunching script...`n"

if (($modeToRun -ieq "run") -or ($modeToRun -ieq "all")) { Run-App }
if (($modeToRun -ieq "analyze") -or ($modeToRun -ieq "all")) { Profile-App }

Write-Host "`nScript finished!`n"
