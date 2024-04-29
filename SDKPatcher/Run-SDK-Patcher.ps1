[CmdletBinding(PositionalBinding=$false)]
Param(
    [switch][Alias('h')]$help,

    [ValidateSet("x86", "x64", "arm32", "arm64")] `
      [string][Alias('arch')]$architecture,

    [ValidateSet("Debug", "Release")] `
      [string][Alias('config')]$configuration,

    [string]$os,
    [string][Alias('repo')]$runtimeRepo,
    [string][Alias('channel')]$sdkChannel,
    [string][Alias('work')]$workPath,

    [switch]$redownload,
    [switch]$rebuild
)

function Display-Help()
{
    Write-Host "Usage Run-SDK-Patcher.ps1 <parameters go here>`n" `
      -ForegroundColor "DarkCyan"

    Write-Host "-h, -help:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Display this message.`n"

    Write-Host "-arch, -architecture:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Architecture the runtime was built for. The allowed values" `
               "are x86, x64, arm32, and arm64.`n"

    Write-Host "-config, -configuration:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Configuration the runtime was built in. The allowed values are" `
               "Debug and Release. Checked will be added in the future.`n"

    Write-Host "-os:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Operating System the runtime was built for.`n"

    Write-Host "-repo, -runtimeRepo:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Path to the runtime repo where the artifacts to be used were built.`n"

    Write-Host "-channel, -sdkChannel:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Distribution channel of the .NET SDK to download and patch. This" `
               "can be either 'main' (the latest nightly build), or 'previewX' where" `
               "`"X`" is the preview version (full numbers only).`n"

    Write-Host "-work, -workPath:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Path where you want to save the downloaded and then patched SDK.`n"

    Write-Host "-redownload:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Delete the existing .NET SDK in `"-workPath`", and download it" `
               "again.`n"

    Write-Host "-rebuild:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Delete the existing build of the SDK Patcher and build it again.`n"
}

# TODO: Validate that the received SDK Channel is correct.

Write-Host "`nRunning Script...!`n"

if ($help)
{
    Display-Help
    exit 0
}

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

$patcherArgs = @($architecture, `
                 $configuration, `
                 $os, `
                 $runtimeRepo, `
                 $workPath, `
                 $redownloadStr, `
                 $sdkChannel)

if (-not ($os -ieq "windows"))
{
    Start-Process -FilePath (Join-Path $patcherOutPath $patcherAppName) `
                  -ArgumentList $patcherArgs `
                  -Wait
}
else
{
    Start-Process -FilePath (Join-Path $patcherOutPath $patcherAppName) `
                  -ArgumentList $patcherArgs `
                  -NoNewWindow `
                  -Wait
}

Write-Host "Finished running the script! Exiting now...`n"
