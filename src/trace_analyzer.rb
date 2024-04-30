# File: trace_analyzer.rb

require 'terminal-table'
require_relative 'utils'

# TODO: Add choices for different trace analyzing functionalities.
class TraceAnalyzer

  # Little class to keep track of the stats of a specific Jitted method.
  class JittedMethodInfo
    attr_reader :name, :time_ms

    def initialize(_methodname, _timems)
      @name = _methodname
      @time_ms = _timems
    end
  end
  private_constant :JittedMethodInfo

  DELIMITER = ' : '
  DETAILEDJIT_STR = 'Detailed JIT Times:'
  WORKERJIT_STR = 'Detailed Language Worker JIT Times:'
  ASMLOADER_STR = 'Detailed Language Worker Assembly Loader Times:'
  private_constant :DELIMITER, :DETAILEDJIT_STR, :WORKERJIT_STR, :ASMLOADER_STR

  attr_reader :analyzerapp, :trace

  def initialize(_analyzer, _trace)
    @analyzerapp = _analyzer.gsub(/\\+/, '/')
    @trace = _trace.gsub(/\\+/, '/')
  end

  def run
    # First, we need to generate the '.coldstart' file with the information,
    # using the trace analyzer app.
    print_banner('Running Analyzer App!')
    print("\n#{@analyzerapp} #{@trace}\n")

    system("#{@analyzerapp} #{@trace}", :out => File::NULL)
    exit_code = $?.exitstatus

    if (exit_code != 0) then
      print("\nSomething went wrong with the analyzer tool :(\n")
      print("EXIT CODE: #{exit_code}")
      return
    end

    # TODO: Here will go a case statement for the different functionalities this
    #       gem script will be able to handle and support.
    display_jitted_methods_table()
  end

  private

  def display_jitted_methods_table
    print_banner('Jitted Methods Numbers!')
    methods = get_jitted_methods()

    methods.map! { |m| [wrap_string(m[0], 80), m[1]] }

    table = Terminal::Table.new do |t|
      t.title = 'Jitted Methods'
      t.headings = ['Method Name', 'Time (msec)']
      t.rows = methods
      t.style = {:width => 110,
                 :padding_left => 1,
                 :border_x => '-',
                 :all_separators => true}
    end

    print("\n#{table}\n")
  end

  def get_jitted_methods
    coldstart_lines = File.open("#{@trace}.coldstart")
                          .readlines
                          .map(&:chomp)

    # The list of jitted methods and their times starts at least one line
    # after the label "Detailed JIT Times:".

    index = coldstart_lines.find_index(DETAILEDJIT_STR) + 1
    index += 1 while coldstart_lines[index].empty?

    # Store all the Jitted Methods information, so that we can afterwards
    # pass it to the terminal table creating gem.

    jit_lines = []

    while (!coldstart_lines[index].empty?)
      jit_lines << coldstart_lines[index]
      index += 1
    end

    jit_lines.map! { |line| line.split(DELIMITER) }
    return jit_lines
  end
end
