#Requires -Version 7.2

[CmdletBinding(PositionalBinding=$false)]
Param(
    [switch][Alias('h')]$help,
    [string][Alias('dotnet')]$dotnetSDKPath,
    [string][Alias('work')]$workPath,
    [switch]$rebuild
)

# /// Display-Help()
# This little function prints a brief message about the script, and a small
# explanation on what each flag does.

function Display-Help()
{
    Write-Host "Usage: BuildAllNET.ps1 <parameters go here>`n" `
      -ForegroundColor "DarkCyan"

    Write-Host "-h, -help:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Display this message.`n"

    Write-Host "-dotnet, -dotnetSDKPath:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Path to the patched .NET SDK to use to build and publish.`n"

    Write-Host "-work, -workPath:" -ForegroundColor "DarkGreen" -NoNewLine
    Write-Host " Path to the directory where all the .NET projects to build are located`n"
}

Write-Host "`nLaunching all-dotnet builder...`n"

if ($help)
{
    Display-Help
    exit 0
}

# We are taking the safe a approach of only looking through the first level.
# This because it's not uncommon to have the base csproj files reference and call
# other project files down the tree, which in said case, do not need to be built
# again directly.

$projectFiles = Get-ChildItem -Path $workPath -Recurse -Depth 1 -Filter "*.csproj"

Write-Host "Projects that will be built:" -ForegroundColor "DarkCyan"
$projectFiles | % { Write-Host "- $($_.FullName)" -ForegroundColor "DarkCyan" }

foreach ($csproj in $projectFiles)
{
    $projFullName = $csproj.FullName
    $projParentDir = Split-Path -Path $projFullName -Parent
    $projFileName = $csproj.Name

    # We're redirecting the 'dotnet build' output to an 'out' folder, instead of the
    # usual 'bin' subtree, for the sake of simplicity.

    $outDir = Join-Path $projParentDir "out"
    $objDir = Join-Path $projParentDir "obj"
    $dotnetArgs = @("build", $projFullName, "-c", "Release", "-o", $outDir)

    # If a rebuild was requested, then delete all artifacts from the previous build
    # prior to calling dotnet again.

    if ($rebuild)
    {
        if (Test-Path -Path $objDir) { Remove-Item -Path $objDir -Recurse -Force }
        if (Test-Path -Path $outDir) { Remove-Item -Path $outDir -Recurse -Force }
    }

    Write-Host "`nBuilding project $projFileName...`n" -ForegroundColor "DarkGreen"

    if ($IsWindows)
    {
        Start-Process -FilePath (Join-Path $dotnetSDKPath "dotnet.exe") `
                      -ArgumentList $dotnetArgs `
                      -NoNewWindow `
                      -Wait
    }
    else
    {
        Start-Process -FilePath (Join-Path $dotnetSDKPath "dotnet") `
                      -ArgumentList $dotnetArgs `
                      -Wait
    }
}

Write-Host "`nFinished building all projects!`n"
