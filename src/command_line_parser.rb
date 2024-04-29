# File: command_line_parser.rb

require 'optparse'

class MainContext
  attr_accessor :sdk, :repo_azfunctions, :workpath
end

class CommandLineParser

  def self.parse_into_context(args)
    context = MainContext.new

    opt_parser = OptionParser.new do |params|
      params.banner = "Usage: ruby azure_functions.rb <options go here>"

      params.on('-h', '--help', 'Prints this message.') do
        puts params
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
    return context
  end

end
