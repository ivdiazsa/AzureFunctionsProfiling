#Requires -Version 7.2

[CmdletBinding(PositionalBinding=$false)]
Param(
    [string][Alias('dotnet')]$dotnetSDKPath,
    [string][Alias('work')]$workPath,
    [switch]$rebuild
)

Write-Host "`nLaunching all-dotnet builder...`n"

# We are taking the safe a approach of only looking through the first level.
# This because it's not uncommon to have the base csproj files reference and call
# other project files down the tree, which in this case, do not need to be built
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
        Start-Process -FilePath (Join-Path $dotnetSDKPath "dotnet.exe") `
                      -ArgumentList $dotnetArgs `
                      -Wait
    }
}

Write-Host "`nFinished building all projects!`n"
