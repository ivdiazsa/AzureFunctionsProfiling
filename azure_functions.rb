#!/usr/bin/ruby

# File: azure_functions.rb

require_relative 'src/command_line_parser'
require_relative 'src/functions_nethost_manager'
require_relative 'src/trace_analyzer'
require_relative 'src/utils'

print_banner("AZURE FUNCTIONS!")
context = CommandLineParser.parse_into_context(ARGV)

# Run each of the desired stages. Currently, this is just symbolic since only one
# stage at a time is supported, but we're laying the groundwork and basis for the
# next steps since now :)

context.stages.each do |stage|
  case stage

  when 'analyze-trace'
    analyzer = TraceAnalyzer.new(
      context.analyzerapp,
      context.tracepath
    )

    analyzer.run()

  when 'build-worker'
    azure_manager = FunctionsNetHostManager.new(
      context.workpath,
      context.repo_azfunctions,
      context.sdk)

    azure_manager.build_repo()

  else
    raise "Got an unexpected stage '#{stage}' :("
  end
end

print_banner("DONE!")
