# File: command_line_parser.rb

require 'optparse'

# TODO: Separate all the MainContext's values into smaller sub-context classes
#       for better code organization and cleanliness.
class MainContext

  attr_reader :analyzetrace_context, :buildworker_context, :runbenchmarks_context,
              :patchsdk_context, :stages

  class AnalyzeTraceContext
    attr_accessor :analyzerapp, :comparetype, :metric, :traces
    def initialize
      @comparetype = nil
      @traces = []
    end
  end

  class BuildWorkerContext
    attr_accessor :azfunctions_repo, :buildonly, :copyonly, :sdk, :workpath
    def initialize
      @buildonly = false
      @copyonly = false
    end
  end

  class RunBenchmarksContext
    attr_accessor :func_harness_path, :perfview, :scenario, :trace
  end

  class PatchSdkContext
    attr_accessor :arch, :config, :os, :runtime_repo, :sdkchannel, :redownload
                  :workpath,
    def initialize
      @redownload = false
    end
  end

  def initialize
    @analyzetrace_context = AnalyzeTraceContext.new
    @buildworker_context = BuildWorkerContext.new
    @runbenchmarks_context = RunBenchmarksContext.new
    @patchsdk_context = PatchSdkContext.new
    @stages = []
  end
end

# TODO: Implement required flags.
class CommandLineParser

  def self.display_help
    puts "\nUsage: ruby azure_functions.rb <stage> <stage-options>"
    puts "\n-h, --help     Prints this message"
    puts "\nThis script contains lots of different functionalities that allow you" \
         " to run all Azure Functions profiling E2E, or just a section of it."     \
         " The currently supported stages are: analyze-trace, build-worker, and"   \
         " run-benchmarks."
    puts "\nRun ruby 'azure_functions.rb <stage> --help' to learn more about each" \
         " individual stage.\n\n"
  end

  def self.parse_into_context(args)
    context = MainContext.new
    stage = args.shift

    if (stage == '-h' or stage == '--help')
      self.display_help()
      exit 0
    end

    # TODO: Add a general help for the script.
    case stage
    when 'analyze-trace'
      self.parse_analyze_trace_params(args, context.analyzetrace_context)
    when 'build-worker'
      self.parse_build_worker_repo_params(args, context.buildworker_context)
    when 'run-benchmarks'
      self.parse_run_benchmarks_params(args, context.runbenchmarks_context)
    when 'patch-sdk'
      self.parse_patch_sdk_params(args, context.patchsdk_context)
    else
      raise "Got an unexpected stage '#{stage}' :("
    end

    # TODO: Implement handling multiple stages.
    context.stages << stage
    return context
  end

  private

  def self.parse_analyze_trace_params(args, ctx)
    opt_parser = OptionParser.new do |params|
      params.banner = "Usage: ruby azure_functions.rb analyze-trace <options go here>"

      params.on('-h', '--help', 'Prints this message.') do
        puts "\n#{params}\n"
        exit 0
      end

      params.on('--analyzer ANALYZER_PATH',
                'Path to the Cold Start analyzer tool executable.') do |value|
        ctx.analyzerapp = value
      end

      params.on('--compare COMP_TYPE',
                'Type of traces comparison (diff, equal, method-times)') do |value|
        ctx.comparetype = value.downcase
      end

      # TODO: Add support for multiple metrics per run.
      params.on('--metric METRIC',
                'Denotes which metric you wish to profile.') do |value|
        ctx.metric = value.downcase
      end

      params.on('--traces',
                '--trace-paths T1,T2',
                Array,
                'Path to the generated ETL traces or coldstart files.') do |value|
        ctx.traces = value
      end
    end
    opt_parser.parse!(args)
  end

  def self.parse_build_worker_repo_params(args, ctx)
    opt_parser = OptionParser.new do |params|
      params.banner = "Usage: ruby azure_functions.rb build-worker <options go here>"

      params.on('-h', '--help', 'Prints this message.') do
        puts "\n#{params}\n"
        exit 0
      end

      params.on('--az-func-repo REPO',
                'Path to the Azure Functions Host and Worker Repo') do |value|
        ctx.azfunctions_repo = value
      end

      # TODO: Ensure '--copy-only' and '--no-copy' are mutually exclusive.
      params.on('--copy-only',
                'Do not rebuild the FunctionsNetHost artifacts. Only copy them.') do
        ctx.copyonly = true
      end

      params.on('--no-copy',
                'Only (re)build the FunctionsNetHost artifacts.') do
        ctx.buildonly = true
      end

      params.on('--sdk', '--patched-sdk SDK', 'Path to the .NET SDK to use') do |value|
        ctx.sdk = value
      end

      params.on('--work WORK_PATH',
                'Path where all the func-harness test stuff is located.') do |value|
        ctx.workpath = value
      end
    end
    opt_parser.parse!(args)
  end

  def self.parse_run_benchmarks_params(args, ctx)
    opt_parser = OptionParser.new do |params|
      params.banner = "Usage: ruby azure_functions.rb run-benchmarks <options go here>"

      params.on('-h', '--help', 'Prints this message.') do
        puts "\n#{params}\n"
        exit 0
      end

      params.on('--func-harness PROFILING_PATH',
                'Path to the func-harness work directory.') do |value|
        ctx.func_harness_path = value
      end

      params.on('--perfview PERFVIEW',
                'Path to the PerfView executable to use.') do |value|
        ctx.perfview = value
      end

      params.on('--scenario SCENARIO',
                'Name of the scenario to run (base, prejit, preload).') do |value|
        ctx.scenario = value.downcase
      end

      params.on('--trace TRACE',
                'Path to the trace to generate with the benchmark.') do |value|
        ctx.trace = value
      end
    end
    opt_parser.parse!(args)
  end

  def self.parse_patch_sdk_params(args, ctx)
    opt_parser = OptionParser.new do |params|
      params.banner = "Usage: ruby azure_functions.rb patch-sdk <options go here>"

      params.on('-h', '--help', 'Prints this message.') do
        puts "\n#{params}\n"
        exit 0
      end

      params.on('--arch ARCHITECTURE',
                'Architecture the runtime was built for.') do |value|
        ctx.arch = value.downcase
      end

      params.on('--config CONFIGURATION',
                'Build configuration of the runtime artifacts.') do |value|
        ctx.config = value.capitalize
      end

      params.on('--os OPERATING_SYSTEM',
                'Operating system the runtime was built for.') do |value|
        ctx.os = value.downcase
      end

      params.on('--redownload',
                'Whether to delete the existing SDK  archive and download it again.') \
      do
        ctx.redownload = true
      end

      params.on('--repo RUNTIME_REPO',
                'Path to the runtime repo clone with the artifacts built.') do |value|
        ctx.runtime_repo = value
      end

      params.on('--channel SDK_CHANNEL',
                'Distibution channel of the SDK to download (main or previewX).') \
      do |value|
        ctx.sdkchannel = value
      end

      params.on('--work WORK_PATH',
                'Path where you want to save the downloaded SDK.') do |value|
        ctx.workpath = value
      end

    end
    opt_parser.parse!(args)
  end

end
