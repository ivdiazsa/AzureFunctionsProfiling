[CmdletBinding(PositionalBinding=$false)]
Param(
    [string][Alias('arch')]$architecture,
    [string][Alias('config')]$configuration,
    [string][Alias('work')]$workPath,
    [string]$os,
    [switch]$redownload,
    [string][Alias('repo')]$runtimeRepo
)

$sdkFolderName = "dotnet-sdk-nightly"

class PatcherContext
{
    [string] $Arch
    [string] $Config
    [string] $NetVersion
    [string] $OS
    [string] $RepoRoot
    [string] $WorkPath

    PatcherContext([hashtable]$properties) {
        foreach ($p in $properties.Keys) { $this.$p = $properties.$p }
    }

    [string] GetPlatform() {
        return "$($this.OS)-$($this.Arch)"
    }

    [string] GetRepoPlatform() {
        return "$($this.OS).$($this.Arch).$($this.Config)"
    }

    [string] GetRepoAltPlatform() {
        return "$($this.GetPlatform()).$($this.Configuration)"
    }

    [string] GetArtifactsBinPath() {
        return (Join-Path $this.RepoRoot "artifacts" "bin")
    }
}

function Extract-CompressedFile([string]$filePath, [string]$destPath)
{
    $extension = (Get-ChildItem $filePath).Extension

    # Powershell has a native functionality to extract zip files, but not others.
    # So, depending on the type of compressed file, we have to choose between using
    # Powershell's native implementation, or call the OS's installed 'tar' utility.

    if ($extension -ieq 'zip')
    {
        Write-Host "Extracting $filePath using Expand-Archive...`n"
        Expand-Archive -Path $filePath -DestinationPath $destPath
    }
    else
    {
        Write-Host "Extracting $filePath using tar utility...`n"
        Start-Process -FilePath "tar" `
                      -ArgumentList @("-zxf", "$filePath", "-C", "$destPath")
    }
}

function DownloadExtract-NightlySDK([PatcherContext]$context)
{
    $zipExt = if ($context.OS -ieq 'windows') { 'zip' } else { 'tar.gz' }
    $downloadZip = (Join-Path $context.WorkPath "dotnet-sdk-nightly.$zipExt")
    $extractFolder = (Join-Path $context.WorkPath $sdkFolderName)

    $nightlyUrl = "https://aka.ms/dotnet/9.0.1xx/daily/" `
                  + "dotnet-sdk-$($context.GetPlatform()).$zipExt"

    # Wanting to download a new SDK nightly build means we want to start an experiment
    # from a clean slate. So, first we delete any remains from previous experiments.

    if (Test-Path -Path $extractFolder)
    {
        Write-Host "Cleaning up $extractFolder..."
        Remove-Item -Path $extractFolder -Recurse -Force
    }

    # Zipped/Compressed downloaded SDK archives are clean, so if we find one there,
    # there is no need to download it again, unless explicitly required with the
    # $redownload flag. For some reason, Azure servers have been really slow in
    # responding to and serving download requests, so we'll take any opportunity
    # to optimize our testing procedures.

    if ($redownload) { Remove-Item -Path $downloadZip -Force }

    if (-not (Test-Path -Path $downloadZip))
    {
        # For some reason, disabling the download progress bar speeds up Powershell
        # downloads by... a lot.
        $ProgressPreference = 'SilentlyContinue'

        Write-Host "Downloading $nightlyUrl...`n"
        Invoke-WebRequest -Uri $nightlyUrl -OutFile $downloadZip
    }

    New-Item -Path $extractFolder -ItemType "directory"
    Extract-CompressedFile -FilePath $downloadZip -DestPath $extractFolder
}

function Patch-SDK([PatcherContext]$context)
{
    $sdkRoot = (Join-Path $context.WorkPath $sdkFolderName)

    # Now, we need to get all the target paths within the SDK where we will copy stuff
    # to, which some contain very specific version numbers like 'preview-4.24204.3'.
    # So, we need to get that string from one of the inner folder names. Any folder
    # in the SDK that contains the full version name is fine. We're using the one
    # in host/fxr here because it's the only one, and therefore we don't need to do
    # any further filtering or processing.

    $sdkVersion = (Get-ChildItem -Path (Join-Path $sdkRoot "host") `
                                 -Directory `
                                 -Recurse `
                                 -Include '9.0.*').Name

    $binsRoot = $context.GetArtifactsBinPath()

    $assemblyPaths =
    @{
        Repo_Coreclr = (Join-Path $binsRoot "coreclr" $context.GetRepoPlatform())
        Repo_Corehost = (Join-Path $binsRoot $context.GetRepoAltPlatform() "corehost")

        Repo_NetcoreAppRef = (Join-Path $binsRoot "microsoft.netcore.app.ref"
                                        "ref" $context.NetVersion)

        Repo_NetcoreAppRuntime = (Join-Path `
          $binsRoot `
          "microsoft.netcore.app.runtime.$($context.GetPlatform())" `
          $context.Configuration "runtimes" `
          $context.GetPlatform() "lib"`
          $context.NetVersion)

        SDK_HostFxr = (Join-Path $sdkRoot "host" "fxr" $sdkVersion)

        SDK_NativePacks = (Join-Path $sdkRoot "packs" `
                                     "Microsoft.NETCore.App.Host.$($context.GetPlatform())" `
                                     $sdkVersion "runtimes" `
                                     $context.GetPlatform() "native")

        SDK_Framework = (Join-Path $sdkRoot "shared" `
                                   "Microsoft.NETCore.App" $sdkVersion)

        SDK_RefPacks = (Join-Path $sdkRoot "packs" `
                                  "Microsoft.NETCore.App.Ref" $sdkVersion `
                                  "ref" $context.NetVersion)
    }

    # Patching HostFxr

    Write-Host "Patching $($assemblyPaths.SDK_HostFxr)/libhostfxr.so..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_Corehost "libhostfxr.so") `
              -Destination (Join-Path $assemblyPaths.SDK_HostFxr "libhostfxr.so")

    # Patching Native Packs

    Write-Host "Patching $($assemblyPaths.SDK_NativePaths)/apphost..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_Corehost "apphost") `
              -Destination (Join-Path $assemblyPaths.SDK_NativePaths "apphost")

    Write-Host "Patching $($assemblyPaths.SDK_NativePaths)/coreclr_delegates.h..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_Corehost "coreclr_delegates.h") `
              -Destination (Join-Path $assemblyPaths.SDK_NativePaths "coreclr_delegates.h")

    Write-Host "Patching $($assemblyPaths.SDK_NativePaths)/hostfxr.h..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_Corehost "hostfxr.h") `
              -Destination (Join-Path $assemblyPaths.SDK_NativePaths "hostfxr.h")

    Write-Host "Patching $($assemblyPaths.SDK_NativePaths)/libnethost.a..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_Corehost "libnethost.a") `
              -Destination (Join-Path $assemblyPaths.SDK_NativePaths "libnethost.a")

    Write-Host "Patching $($assemblyPaths.SDK_NativePaths)/libnethost.so..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_Corehost "libnethost.so") `
              -Destination (Join-Path $assemblyPaths.SDK_NativePaths "libnethost.so")

    Write-Host "Patching $($assemblyPaths.SDK_NativePaths)/nethost.h..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_Corehost "nethost.h") `
              -Destination (Join-Path $assemblyPaths.SDK_NativePaths "nethost.h")

    Write-Host "Patching $($assemblyPaths.SDK_NativePaths)/singlehost..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_Corehost "apphost") `
              -Destination (Join-Path $assemblyPaths.SDK_NativePaths "apphost")

    # Patching Framework

    Write-Host "Patching $($assemblyPaths.SDK_Framework)/libhostpolicy.so..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_Corehost "libhostpolicy.so") `
              -Destination (Join-Path $assemblyPaths.SDK_Framework "libhostpolicy.so")

    Write-Host "Patching $($assemblyPaths.SDK_Framework)/libcoreclr.so..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_Coreclr "libcoreclr.so") `
              -Destination (Join-Path $assemblyPaths.SDK_Framework "libcoreclr.so")

    Write-Host "Patching $($assemblyPaths.SDK_Framework)/System.Private.CoreLib.dll..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_Coreclr "System.Private.CoreLib.dll") `
              -Destination (Join-Path $assemblyPaths.SDK_Framework "System.Private.CoreLib.dll")

    Write-Host "Patching $($assemblyPaths.SDK_Framework)/System.Runtime.dll..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_NetcoreAppRuntime "System.Runtime.dll") `
              -Destination (Join-Path $assemblyPaths.SDK_Framework "System.Runtime.dll")

    # Patching Native Refs

    Write-Host "Patching $($assemblyPaths.SDK_RefPacks)/System.Runtime.dll..."
    Copy-Item -Path (Join-Path $assemblyPaths.Repo_NetcoreAppRef "System.Runtime.dll") `
      -Destination (Join-Path $assemblyPaths.SDK_RefPacks "System.Runtime.dll")

    Write-Host "`nFinished patching the SDK!`n"
}

# *************************
# Main Script Starts Here!
# *************************

Write-Host "`nLaunching Script...!`n"

$context = [PatcherContext]::new(
    @{
        Arch = $architecture.ToLower()
        Config = (Get-Culture).TextInfo.ToTitleCase($configuration.ToLower())
        NetVersion = "net9.0"
        OS = $os.ToLower()
        RepoRoot = $runtimeRepo
        WorkPath = $workPath
    })

DownloadExtract-NightlySDK -Context $context
Patch-SDK -Context $context
