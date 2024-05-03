# File: functions_nethost_manager.rb

require 'fileutils'
require 'pathname'
require_relative 'utils'

class FunctionsNetHostManager

  attr_reader :dotnetexe, :profiling_artifactspath, :repo, :sdk

  @@execext = ENV['OS'].casecmp?('Windows_NT') ? '.exe' : ''
  @@buildpath = Pathname.new('bin/Release/net9.0')
  @@publishpath = Pathname.new('bin/Release/net9.0/win-x64/publish')

  # Backslashes in paths at best make them look a bit ugly, and at worst render
  # them unusable. So, let's convert all the paths we get to use forward slashes
  # before working with them.

  def initialize(_profilingpath, _repo, _sdk)
    @profiling_artifactspath = Pathname.new(_profilingpath.gsub(/\\+/, '/')) \
      unless _profilingpath.nil?

    @repo = Pathname.new(_repo.gsub(/\\+/, '/')) unless _repo.nil?
    @sdk = Pathname.new(_sdk.gsub(/\\+/, '/')) unless _sdk.nil?
    @dotnetexe = @sdk.join("dotnet#{@@execext}") unless @sdk.nil?
  end

  def build_repo
    build_devpack()
    build_functions_nethost()
    build_placeholder_apps()
  end

  def copy_artifacts_to_work_zone
    print_banner("Copying FunctionsNetHost Artifacts!")

    # Define the paths we will need to be referencing during the copying process.

    app44_name = 'FunctionApp44'
    placeholder_name = 'PlaceholderApp'
    startuphook_name = 'StartupHook'

    # The '+' sign here is an alias for Pathname's join() method.
    hostsrc_bins = @repo + 'host/src/FunctionsNetHost' + @@publishpath
    app44_bins = @repo + 'samples' + app44_name + @@buildpath
    place_hook_bins = @repo + 'samples' + placeholder_name + @@buildpath

    # First, we have to create the directory tree of the func-harness work zone
    # we will use for our experiments.

    if (Dir.exist?(@profiling_artifactspath))
      FileUtils.remove_entry_secure(@profiling_artifactspath, force: true)
    end

    app44_path = @profiling_artifactspath + "#{app44_name}Base"
    nethostbase_path = @profiling_artifactspath + 'FunctionsNetHostBase'
    nethostprejit_path = @profiling_artifactspath + 'FunctionsNetHostPreJitOnly'

    print("\nCreating #{@profiling_artifactspath} folder...\n")
    FileUtils.mkdir_p(@profiling_artifactspath)

    print("Creating #{app44_name}Base folder...\n")
    FileUtils.mkdir_p("#{app44_path}")

    print("Creating FunctionsNetHostBase folder tree...\n")
    make_many_dirs("#{nethostbase_path}/#{placeholder_name}",
                   "#{nethostbase_path}/#{startuphook_name}")

    print("Creating FunctionsNetHostPreJit folder tree...\n")
    make_many_dirs("#{nethostprejit_path}/#{app44_name}",
                   "#{nethostprejit_path}/#{placeholder_name}",
                   "#{nethostprejit_path}/#{startuphook_name}")

    # Next, we have to copy the binaries we built in the repo to their respective
    # spots in the work zone.

    print("\nCopying FunctionsNetHost binaries...\n")

    copy_one_to_many("#{hostsrc_bins}/FunctionsNetHost.exe",
                     nethostbase_path, nethostprejit_path)
    
    copy_one_to_many("#{hostsrc_bins}/nethost.dll",
                     nethostbase_path, nethostprejit_path)
    
    print("Copying #{app44_name} binaries...\n")

    copy_one_to_many("#{app44_bins}/.",
                     app44_path, "#{nethostprejit_path}/#{app44_name}")
    
    print("Copying #{placeholder_name} binaries...\n")

    copy_one_to_many(Dir.glob("#{place_hook_bins}/PlaceholderApp.*"),
                     "#{nethostbase_path}/#{placeholder_name}",
                     "#{nethostprejit_path}/#{placeholder_name}")

    print("Copying #{placeholder_name} binaries...\n")

    copy_one_to_many(Dir.glob("#{place_hook_bins}/StartupHook.*"),
                     "#{nethostbase_path}/#{startuphook_name}",
                     "#{nethostprejit_path}/#{startuphook_name}")

    print("\nFinished copying the artifacts!\n")
  end

  private

  def build_devpack
    devpack_ps = @repo.join('tools/devpack.ps1')
    devpack_args = "-PatchedDotnet \"#{@dotnetexe}\""
    pwsh_args = "-NoProfile #{devpack_ps} #{devpack_args}"
    command = "pwsh #{pwsh_args}"

    print_banner("Building Functions DevPack!")
    print("\n#{command}\n\n")
    system(command)
  end

  def build_functions_nethost
    # TODO: Handle the platform universally.
    # We need the absolute path for the dotnet command because we need to be in
    # the host/src directory to run it. If we have a relative path, it is relative
    # to elsewhere, so it wouldn't work here.

    hostsrc = @repo.join('host/src')
    publish_args = "publish -c Release -r win-x64"
    command = "#{File.absolute_path(@dotnetexe)} #{publish_args}"

    Dir.chdir(hostsrc) do
      print_banner("Building and publishing Functions NetHost!")
      print("\n#{command}\n\n")
      system(command)
    end
  end

  def build_placeholder_apps
    samples = @repo.join("samples")

    csproj_files = [
      samples.join('PlaceholderApp/PlaceholderApp.csproj'),
      samples.join('PlaceholderApp/StartupHook.csproj')
    ]

    print_banner("Building sample apps!")

    csproj_files.each do |proj|
      build_args = "build #{proj} -c Release"
      command = "#{@dotnetexe} #{build_args}"

      print("\nBuilding '#{proj.basename}'...\n")
      print("\n#{command}\n\n")
      system(command)
    end
  end

end
