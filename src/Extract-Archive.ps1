[CmdletBinding(PositionalBinding=$false)]
Param(
    [string]$srcCompressed,
    [string]$targetDest
)

$compressedFilename = Split-Path -Path $srcCompressed -Leaf
Write-Host "Extracting $compressedFilename using Powershell's Expand-Archive..."
Expand-Archive -Path $srcCompressed -DestinationPath $targetDest
