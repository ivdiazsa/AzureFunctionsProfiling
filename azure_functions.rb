#!/usr/bin/ruby

# File: azure_functions.rb

require_relative 'src/command_line_parser'
require_relative 'src/functions_nethost_manager'
require_relative 'src/utils'

print_banner("AZURE FUNCTIONS!")
context = CommandLineParser.parse_into_context(ARGV)

azure_manager = FunctionsNetHostManager.new(
  context.workpath,
  context.repo_azfunctions,
  context.sdk)

azure_manager.build_repo()
print_banner("DONE!")
