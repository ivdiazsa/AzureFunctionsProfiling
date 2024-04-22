[CmdletBinding(PositionalBinding=$false)]
Param(
    [string][Alias('arch')]$architecture,
    [string][Alias('config')]$configuration,
    [string]$os,
    [string][Alias('repo')]$runtimeRepo,
    [string][Alias('work')]$workPath,
    [switch]$redownload,
    [switch]$rebuild
)

Write-Host "`nRunning Script...!`n"

$architecture = $architecture.ToLower()
$configuration = (Get-Culture).TextInfo.ToTitleCase($configuration.ToLower())
$os = $os.ToLower()
$redownloadStr = if ($redownload) { "true" } else { "false" }

$patcherObjPath = (Join-Path $PSScriptRoot "obj")
$patcherOutPath = (Join-Path $PSScriptRoot "out")
$patcherAppName = if ($os -ieq "windows") { "SDKPatcher.exe" } else { "SDKPatcher" }

# If rebuild was requested, delete the previous build's out and obj directories,
# and print a nice message about going to rebuild the app :)

if ($rebuild)
{
    if (Test-Path $patcherOutPath) { Remove-Item -Path $patcherOutPath -Recurse -Force }
    if (Test-Path $patcherObjPath) { Remove-Item -Path $patcherObjPath -Recurse -Force }

    Write-Host "Rebuild flag was passed, so will build the SDK Patcher again...`n" `
               -ForegroundColor "DarkYellow"
}

# Build the SDK Patcher app if it's not there, or a rebuild was requested.

if ((-not (Test-Path (Join-Path $patcherOutPath $patcherAppName))))
{
    if (-not $rebuild) { Write-Host "SDK Patcher app not found. Building it now...`n" }

    if (-not ($os -ieq "windows"))
    {
        Start-Process -FilePath "dotnet" `
                      -ArgumentList @("build", "-c", "Release", "-o", "out", "-tl:off") `
                      -Wait
    }
    else
    {
        Start-Process -FilePath "dotnet" `
                      -ArgumentList @("build", "-c", "Release", "-o", "out", "-tl:off") `
                      -NoNewWindow `
                      -Wait
    }
}
else
{
    Write-Host "SDK Patcher app found. Running it now..." -ForegroundColor "Green"
}

# Run the patcher app with the processed parameters from here.

if (-not ($os -ieq "windows"))
{
    Start-Process -FilePath (Join-Path $patcherOutPath $patcherAppName) `
                  -ArgumentList `
                  @($architecture, `
                  $configuration, `
                  $os, `
                  $runtimeRepo, `
                  $workPath, `
                  $redownloadStr) `
                  -Wait
}
else
{
    Start-Process -FilePath (Join-Path $patcherOutPath $patcherAppName) `
                  -ArgumentList `
                  @($architecture, `
                  $configuration, `
                  $os, `
                  $runtimeRepo, `
                  $workPath, `
                  $redownloadStr) `
                  -NoNewWindow `
                  -Wait
}

Write-Host "Finished running the script! Exiting now...`n"
