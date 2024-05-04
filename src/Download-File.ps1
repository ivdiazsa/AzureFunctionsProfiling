[CmdletBinding(PositionalBinding=$false)]
Param(
    [string]$srcUrl,
    [string]$targetDest
)

Write-Host "Downloading $srcUrl to $targetDest..."
Invoke-WebRequest -Uri $srcUrl -OutFile $targetDest
