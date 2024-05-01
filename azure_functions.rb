#!/usr/bin/ruby

# File: azure_functions.rb

require_relative 'src/benchmark_runner'
require_relative 'src/command_line_parser'
require_relative 'src/functions_nethost_manager'
require_relative 'src/trace_analyzer'
require_relative 'src/utils'

print_banner("AZURE FUNCTIONS!")
context = CommandLineParser.parse_into_context(ARGV)

# TODO: Add some flags validation, e.g. can't compare if only one trace was provided.

# Run each of the desired stages. Currently, this is just symbolic since only one
# stage at a time is supported, but we're laying the groundwork and basis for the
# next steps since now :)

context.stages.each do |stage|
  case stage
  when 'analyze-trace'
    analyzer = TraceAnalyzer.new(
      context.analyzerapp,
      context.tracepaths
    )

    # This is still a WIP. Comparing traces requires more configuration than what
    # we normally can have when including it as a value for a universal analysis
    # kind flag. Hence, we need to handle it separately.

    analysis_kind = context.analysisparams.has_key?(:compare)       ?
                      "compare-#{context.analysisparams[:compare]}" :
                      'table-display'

    analyzer.run(analysis_kind, context.analysisparams[:metric])

  when 'build-worker'
    azure_manager = FunctionsNetHostManager.new(
      context.workpath,
      context.repo_azfunctions,
      context.sdk)

    azure_manager.build_repo()

  when 'run-benchmarks'
    benchmarker = BenchmarkRunner.new(
      context.func_harness_work,
      context.perfview,
      context.scenario,
      context.tracepath
    )

    benchmarker.run()

  else
    raise "Got an unexpected stage '#{stage}' :("
  end
end

print_banner("DONE!")
