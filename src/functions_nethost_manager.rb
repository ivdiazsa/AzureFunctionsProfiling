# File: functions_nethost_manager.rb

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
    @profiling_artifactspath = Pathname.new(_profilingpath.gsub(/\\+/, '/'))
    @repo = Pathname.new(_repo.gsub(/\\+/, '/'))
    @sdk = Pathname.new(_sdk.gsub(/\\+/, '/'))
    @dotnetexe = @sdk.join("dotnet#{@@execext}")
  end

  # NEXT UP: Implement copying the binaries to the work area.

  def build_repo
    build_devpack()
    build_functions_nethost()
    build_placeholder_apps()
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
    hostsrc = @repo.join('host/src')
    publish_args = "publish -c Release -r win-x64"
    command = "#{@dotnetexe} #{publish_args}"

    print_banner("Building and publishing Functions NetHost!")
    print("\n#{command}\n\n")
    system(command)
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
