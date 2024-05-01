# File: benchmark_runner.rb

require_relative 'utils'

class BenchmarkRunner

  attr_reader :funcharnesspath, :perfviewpath, :scenario, :tracepath

  def initialize(_funcharness, _perfview, _scenario, _trace)
    @funcharnesspath = _funcharness.gsub(/\\+/, '/')
    @perfviewpath = _perfview.gsub(/\\+/, '/')
    @scenario = _scenario
    @tracepath = _trace.gsub(/\\+/, '/')
  end

  def run
    print_banner("Running #{@scenario.capitalize} Benchmarks!")

    # We use Ruby's chdir() block here because we want to return to whichever
    # directory the user was in, when this script was called. Just to keep the
    # user's environment consistent and undisrupted.

    Dir.chdir(@funcharnesspath) do
      print("\nSet cwd to '#{@funcharnesspath}'...\n")

      # JSON settings filenames we will require.
      runner_settings = 'harness.settings.json'
      backup_settings = 'harness.settings_backup.json'

      scenario_settings = @scenario == 'base' ?
                            'harness.settings_base.json' :
                            "harness.settings_base+#{@scenario}.json"

      # Check if there is already a 'harness.settings.json' file. If yes, then we need
      # to rename it to make space for the scenario we want to run. This is because the
      # 'func-harness' tool was written to always search for a file called 'harness.settings.json'.

      if (File.exist?(runner_settings))
        print("Renaming #{runner_settings} to #{backup_settings}...\n")
        File.rename(runner_settings, backup_settings)
      end

      print("Renaming #{scenario_settings} to #{runner_settings}...\n")
      File.rename(scenario_settings, runner_settings)

      # Azure Functions gave us some specific providers to capture the right
      # profiling data.
      providers = ["DCCCCC7B-F393-4852-96AE-BB6769A266C4",
                   "E30BA2D3-75B8-4E96-9F82-F41EAAC243E5",
                   "69CB2C45-CAF6-48C1-81F9-4C59D93CF43B"]

      # perfview_args = "/DataFile:\"#{@tracepath}\""            \
      #                 " /BufferSizeMB:256"                     \
      #                 " /LogFile:#{@tracepath}.log"            \
      #                 " /StackCompression"                     \
      #                 " /CircularMB:500"                       \
      #                 " /Providers:\"#{providers.join(',')}\"" \
      #                 " /NoGui"                                \
      #                 " /NoNGenRundown"                        \
      #                 " /Merge:True"                           \
      #                 " /Zip:False"                            \
      #                 " run"                                   \
      #                 " func-harness"

      perfview_args = ["/DataFile:\"#{@tracepath}\"",
                       "/BufferSizeMB:256",
                       "/LogFile:#{@tracepath}.log",
                       "/StackCompression",
                       "/CircularMB:500",
                       "/Providers:\"#{providers.join(',')}\"",
                       "/NoGui",
                       "/NoNGenRundown",
                       "/Merge:True",
                       "/Zip:False",
                       "run",
                       "func-harness"]

      # puts "PROVIDERS: #{providers}"
      # puts ""
      # puts "ARGS: #{perfview_args.join(' ')}"
      # return

      print("\nRunning #{@perfviewpath} #{perfview_args.join(' ')}\n\n")
      system("#{perfviewpath} #{perfview_args.join(' ')}")

      # Once everything's done, restore the folder/files structure to how it was.

      print("\nRestoring #{runner_settings} to #{scenario_settings}...\n")
      File.rename(runner_settings, scenario_settings)

      print("Restoring #{backup_settings} tp #{runner_settings}...\n")
      File.rename(backup_settings, runner_settings)
    end
  end
end
