# File: sdk_patcher.rb

require 'pathname'
require 'uri'
require_relative 'utils'

class SdkPatcher

  BASE_SDK_ROOT_FOLDER = 'dotnet-sdk-nightly'
  NET_VERSION = 'net9.0'

  private_constant :BASE_SDK_ROOT_FOLDER, :NET_VERSION

  attr_reader :arch, :config, :os, :reporoot, :sdkchannel, :workpath
  attr_reader :artifactsbin_path, :platform, :repoplatform, :repoaltplatform

  attr_accessor :sdkroot

  def initialize(_arch, _config, _os, _repo, _sdkchannel, _work)
    @arch = _arch
    @config = _config
    @os = _os
    @sdkchannel = _sdkchannel
    @reporoot = Pathname.new(_repo.gsub(/\\+/, '/'))
    @workpath = Pathname.new(_work.gsub(/\\+/, '/'))

    @sdkroot = @workpath + BASE_SDK_ROOT_FOLDER

    @platform = @os == 'windows' ? "win-#{@arch}" : "#{@os}-#{@arch}"
    @repoplatform = "#{@os}.#{@arch}.#{@config}"
    @repoaltplatform = "#{@platform}.#{@config}"
    @artifactsbin_path = @reporoot.join('artifacts/bin')
  end

  def patch_sdk
    print_banner('Patching the SDK!')

    # Now, we need to get all the target paths within the SDK where we will
    # copy stuff to, which some contain very specific version numbers like
    # 'preview-4.24204.3'. So, we need to get that string from one of the
    # inner folder names. Any folder in the SDK that contains the full version
    # name is fine. We're using the one in host/fxr here because it's only one
    # there, and therefore we don't need to do any further filtering or processing.
    sdkversion = Dir.glob("#{@sdkroot}/host/fxr/9.0.*")[0].split('/')[-1]

    netappref_s = 'Microsoft.NETCore.App.Ref'
    netapp_s = 'Microsoft.NETCore.App'
    netapphost_s = 'Microsoft.NETCore.App.Host'

    bins = @artifactsbin_path

    paths = {
      :repo_coreclr => bins + "coreclr/#{@repoplatform}",
      :repo_corehost => bins + "#{@repoaltplatform}/corehost",
      :repo_netcoreappref => bins + "#{netappref_s.downcase}/ref" + NET_VERSION,

      :repo_netcoreappruntime => \
      bins + "#{netapp_s.downcase}.runtime.#{@platform}/#{@config}/runtimes" \
      + "#{@platform}/lib" + NET_VERSION,

      :sdk_hostfxr => @sdkroot + "host/fxr/#{sdkversion}",
      :sdk_framework => @sdkroot + "shared/#{netapp_s}/#{sdkversion}",
      :sdk_refpacks => @sdkroot + "packs/#{netappref_s}/#{sdkversion}/ref" + NET_VERSION,

      :sdk_nativepacks => \
      @sdkroot + "packs/#{netapphost_s}.#{@platform}/#{sdkversion}/runtimes" \
      + "#{@platform}/native"
    }

    # TODO: Add file extensions handling when we add support for the other platforms.

    print("\nPatching hostfxr...\n")
    copy_many_to_one(paths[:sdk_hostfxr], "#{paths[:repo_corehost]}/hostfxr.dll")

    print("Patching native packs...\n")
    copy_many_to_one(paths[:sdk_nativepacks],
                     "#{paths[:repo_corehost]}/apphost.exe",
                     "#{paths[:repo_corehost]}/coreclr_delegates.h",
                     "#{paths[:repo_corehost]}/hostfxr.h",
                     "#{paths[:repo_corehost]}/nethost.lib",
                     "#{paths[:repo_corehost]}/nethost.dll",
                     "#{paths[:repo_corehost]}/nethost.h",
                     "#{paths[:repo_corehost]}/singlefilehost.exe")

    # TODO: This will need to be conditioned to Windows only when we add support
    #       for the other platforms.

    copy_many_to_one(paths[:sdk_nativepacks],
                    "#{paths[:repo_corehost]}/comhost.dll",
                    "#{paths[:repo_corehost]}/ijwhost.dll",
                    "#{paths[:repo_corehost]}/ijwhost.lib",
                    "#{paths[:repo_corehost]}/libnethost.lib",
                    "#{paths[:repo_corehost]}/PDB/libnethost.pdb")

    print("Patching framework...\n")
    copy_many_to_one(paths[:sdk_framework],
                     "#{paths[:repo_corehost]}/hostpolicy.dll",
                     "#{paths[:repo_coreclr]}/coreclr.dll",
                     "#{paths[:repo_coreclr]}/clrgcexp.dll",
                     "#{paths[:repo_coreclr]}/clrgc.dll",
                     "#{paths[:repo_coreclr]}/clrjit.dll",
                     "#{paths[:repo_coreclr]}/System.Private.CoreLib.dll",
                     "#{paths[:repo_netcoreappruntime]}/System.Runtime.dll")

    # TODO: This will also need to be conditioned to Windows only when we add
    #       support for the other platforms.

    copy_many_to_one(paths[:sdk_framework], "#{paths[:repo_coreclr]}/clretwrc.dll")

    print("Patching native refs...\n")
    copy_many_to_one(paths[:sdk_refpacks],
                     "#{paths[:repo_netcoreappref]}/System.Runtime.dll")

    print("\nFinished patching the SDK!\n")
  end

  def downloadx_nightly_sdk(redownload)
    print_banner("Downloading the Nightly SDK!")

    zipext = @os == 'windows' ? '.zip' : '.tar.gz'
    channel = @sdkchannel == 'main' ? "" : "-#{@sdkchannel}"
    downloadzip = @workpath + "dotnet-sdk-nightly-#{@platform}-#{@sdkchannel}#{zipext}"

    @sdkroot = "#{@sdkroot}#{channel}"

    # If the directory where we will extract the downloaded SDK already exists,
    # then, we have to come up with another name to avoid overwriting and/or
    # deleting the existing one. This, because the user might want to be testing
    # different builds in parallel.

    if (Dir.exist?(@sdkroot))
      i = 1
      while (Dir.exist?("#{@sdkroot}-#{i}")) do i += 1 end

      print("\n#{@sdkroot.basename} folder exists."
            " Will extract to #{@sdkroot.basename}-#{i} instead.\n")
      @sdkroot = "#{@sdkroot}-#{i}"
    end

    nightly_url = "https://aka.ms/dotnet/9.0.1xx#{channel}/daily/" \
                  "dotnet-sdk-#{@platform}#{zipext}"

    # Zipped/Compressed downloaded SDK archives are clean, so if we find one
    # here, there is no need to download it again, unless explicitly required
    # with the Redownload flag. For some reason, Azure servers have been really
    # slow in responding to and serving download requests, so we'll take any
    # opportunity to optimize our testing procedures.

    if (File.exist?(downloadzip) and redownload)
      print("\nRedownload flag found and #{downloadzip.basename} exists."
            " Cleaning it up...\n")
      File.delete(downloadzip)
    end

    if (!File.exist?(downloadzip)) then download_file(nightly_url, downloadzip)
    else print("\nFound #{downloadzip.basename}. Continuing...\n") end

    # Extract compressed file here.
  end

  private

  def download_file(url, savepath)
    print("\nDownloading #{url} to #{savepath}...\n")
    dl_stream = URI.open(url)
    File.write(savepath, dl_stream)
  end
end
