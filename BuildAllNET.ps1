#Requires -Version 7.2

[CmdletBinding(PositionalBinding=$false)]
Param(
    [string][Alias('dotnet')]$dotnetSDKPath,
    [string][Alias('work')]$workPath,
    [switch]$rebuild
)

Write-Host "`nLaunching all-dotnet builder...`n"

$projectFiles = Get-ChildItem -Path $workPath -Recurse -Depth 1 -Filter "*.csproj"

Write-Host "Projects that will be built:" -ForegroundColor "DarkCyan"
$projectFiles | % { Write-Host "- $($_.FullName)" -ForegroundColor "DarkCyan" }

foreach ($csproj in $projectFiles)
{
    $projFullName = $csproj.FullName
    $projParentDir = Split-Path -Path $projFullName -Parent
    $projFileName = $csproj.Name

    $outDir = Join-Path $projParentDir "out"
    $objDir = Join-Path $projParentDir "obj"
    $dotnetArgs = @("build", $projFullName, "-c", "Release", "-o", $outDir)

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
