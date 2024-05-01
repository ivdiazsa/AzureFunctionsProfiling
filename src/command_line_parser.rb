# File: command_line_parser.rb

require 'optparse'

class MainContext
  attr_accessor :analysisparams, :analyzerapp, :repo_azfunctions, :sdk, :stages,
                :tracepaths, :workpath

  def initialize
    @analysisparams = {}
    @tracepaths = []
    @stages = []
  end
end

# TODO: Implement required flags.
class CommandLineParser

  def self.parse_into_context(args)
    context = MainContext.new
    stage = args.shift

    # TODO: Add a general help for the script.
    case stage
    when 'analyze-trace'
      self.parse_analyze_trace_params(args, context)
    when 'build-worker'
      self.parse_build_worker_repo_params(args, context)
    else
      raise "Got an unexpected stage '#{stage}' :("
    end

    # TODO: Implement handling multiple stages.
    context.stages << stage
    return context
  end

  private

  def self.parse_analyze_trace_params(args, context)
    opt_parser = OptionParser.new do |params|
      params.banner = "Usage: ruby azure_functions.rb analyze-trace <options go here>"

      params.on('-h', '--help', 'Prints this message.') do
        puts "\n#{params}\n"
        exit 0
      end

      params.on('--analyzer',
                '--analyzer-path ANALYZER',
                'Path to the Cold Start analyzer tool executable.') do |value|
        context.analyzerapp = value
      end

      params.on('--compare COMP', 'Compare traces stuff. Will update this doc later.') do |value|
        context.analysisparams[:compare] = value
      end

      # TODO: Add support for multiple metrics per run.
      params.on('--metric',
                '--metric-kind KIND',
                'Denotes which metric you wish to profile.') do |value|
        context.analysisparams[:metric] = value.downcase
      end

      params.on('--traces',
                '--trace-paths T1,T2',
                Array,
                'Path to the generated ETL traces or coldstart files.') do |value|
        context.tracepaths = value
      end
    end
    opt_parser.parse!(args)
  end

  def self.parse_build_worker_repo_params(args, context)
    opt_parser = OptionParser.new do |params|
      params.banner = "Usage: ruby azure_functions.rb build-worker <options go here>"

      params.on('-h', '--help', 'Prints this message.') do
        puts "\n#{params}\n"
        exit 0
      end

      params.on('--az-func-repo REPO',
                'Path to the Azure Functions Host and Worker Repo') do |value|
        context.repo_azfunctions = value
      end

      params.on('--sdk', '--patched-sdk SDK', 'Path to the .NET SDK to use') do |value|
        context.sdk = value
      end

      params.on('--work',
                '--work-path PATH',
                'Path where all the func-harness test stuff is located.') do |value|
        context.workpath = value
      end
    end
    opt_parser.parse!(args)
  end

end
