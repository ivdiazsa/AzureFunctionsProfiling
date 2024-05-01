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

  attr_reader :analyzerapp, :traces

  # We can actually not receive an analyzer path. In that case, the trace files
  # must all be already processed .coldstart files.
  # TODO: Add validation for the above comment.

  def initialize(_analyzer, _traces)
    @analyzerapp = _analyzer.gsub(/\\+/, '/') unless _analyzer.nil?
    @traces = _traces.map { |tpath| tpath.gsub(/\\+/, '/') }
  end

  def run(analysis_kind, metric)
    print_banner('Running Analyzer App!')

    # We can receive either a trace or its already processed .coldstart file,
    # or a combination of both. So, we check each of them, so we run the analysis
    # tool for those that are .etl/.etlx files, and generate their respective
    # .coldstart files to work with.

    @traces.each_with_index do |tfile, tindex|
      # Is a coldstart file, then it's ready to go.
      if (tfile.split('.')[-1] == 'coldstart') then next end

      # Otherwise, run the analyzer tool and generate it.
      print("\n#{tfile} is a trace file. Running the analyzer tool to generate" \
            " its respective coldstart file.")
      print("\n#{@analyzerapp} #{tfile}\n")
      system("#{@analyzerapp} #{tfile}", :out => File::NULL)
      exit_code = $?.exitstatus

      if (exit_code != 0) then
        print("\nSomething went wrong with the analyzer tool :(\n")
        print("EXIT CODE: #{exit_code}")
        return
      end

      @traces[tindex] = "#{tfile}.coldstart"
    end

    # Next, the path we'll take for each trace.coldstart file, depends on what kind
    # of analysis we want to perform.
    #
    # - Table Display: Show the given metric's values in a nicely formatted table.
    #                  Potential values are: Jit, Worker Jit, and Worker Asm Loader.
    #
    # - Compare Diff: Show which methods/assemblies differ between the given traces
    #                 on the given metric.
    #
    # - Compare Equal: Show which methods/assemblies are the same between the given
    #                  traces on the given metric.
    #
    # - Compare Method Times: Show each jitted method the traces have in common, and
    #                         the time it took on each one respectively.

    case analysis_kind
    when 'table-display'
      @traces.each { |tfile| display_metrics_table(metric, tfile) }
    when 'compare-diff'
      compare_traces(metric, :diff)
    when 'compare-equal'
      compare_traces(metric, :equal)
    when 'compare-method-times'
      compare_traces(metric, :methodtimes)
    end
  end

  private

  def display_metrics_table(metric, coldstart)
    print_banner('Jitted Methods Numbers!')
    puts metric

    str_mark = case metric
               when 'jit' then DETAILEDJIT_STR
               when 'worker-jit' then WORKERJIT_STR
               when 'worker-asm-loader' then ASMLOADER_STR
               end

    methods = get_methods_assemblies_data(str_mark, coldstart)

    title = metric == 'worker-asm-loader' ? 'Loaded Assemblies' : 'Jitted Methods'
    headings = metric == 'worker-asm-loader'    ?
                 ['Assembly Name', 'Time (ms)'] :
                 ['Method Name', 'Time (ms)']

    generate_table(methods, title, headings)
  end

  def compare_traces(metric, comptype)
    print_banner('Comparing Traces!')

    # First things first. We need to get the data from each trace's coldstart file.
    data = []
    str_mark = case metric
               when 'jit' then DETAILEDJIT_STR
               when 'worker-jit' then WORKERJIT_STR
               when 'worker-asm-loader' then ASMLOADER_STR
               end
    
    @traces.each { |coldstart| data << get_methods_assemblies_data(str_mark, coldstart) }
    trace_names = @traces.map { |path| path.split('/')[-1] }

    # Using hashes will be essential to easily find the sames and differences between
    # the traces.
    data.map!(&:to_h)

    # Little snippet I used to confirm all the values were indeed equal.

    # test1 = data[0].keys.sort
    # test2 = data[1].keys.sort
    # generate_table(test1.zip(test2), 'Testing if they are really equal', ['One', 'Two'])
    # return

    # TODO: Support comparing more than two traces at once.

    case comptype
    when :equal
      result = data[0].keep_if { |k, _v| data[1].has_key?(k) }.keys
      s = metric == 'worker-asm-loader' ? 'Assemblies' : 'Jitted Methods'

      print("\nTraces '#{trace_names[0]}' and '#{trace_names[1]}' share the" \
            " following #{s}:\n\n")
      result.each { |item| puts "- #{item}" }
      print("\n")

    when :diff
      only_in_first = data[0].keys.difference(data[1].keys)
      only_in_second = data[1].keys.difference(data[0].keys)

      firstlen = only_in_first.length
      secondlen = only_in_second.length

      # It's not unlikely that the amount of methods unique to one trace will be
      # the same amount of methods unique to the other trace. However, the
      # TerminalTable gem requires all rows have the same amount of columns. So,
      # we fill out the smaller one with an empty value instead for this purpose.

      if (firstlen < secondlen)
        only_in_first.fill(firstlen..(secondlen - 1)) { "" }
      elsif (secondlen < firstlen)
        only_in_second.fill(secondlen..(firstlen - 1)) { "" }
      end

      rows = only_in_first.zip(only_in_second)
      title = metric == 'worker-asm-loader' ?
                'Different Assemblies'      :
                'Different Jitted Methods'

      headings = [trace_names[0], trace_names[1]]
      generate_table(rows, title, headings)

    when :methodtimes
      # TODO: Fix the hashes in case they have different amount of elements.
      result = []
      data[0].each_pair do |key, value|
        result << [key, value, data[1][key]]
      end

      title = 'Jitted Method Times'
      headings = ['Method Name', trace_names[0], trace_names[1]]
      generate_table(result, title, headings)
    end
  end

  def get_methods_assemblies_data(str_mark, file)
    coldstart_lines = File.open(file)
                          .readlines
                          .map(&:chomp)

    # The list of jitted methods and their times starts at least one line
    # after the label "Detailed JIT Times:".

    index = coldstart_lines.find_index(str_mark) + 1
    index += 1 while coldstart_lines[index].empty?

    # Store all the Jitted Methods information, so that we can afterwards
    # pass it to the terminal table creating gem.

    data_lines = []

    while (!coldstart_lines[index].empty?)
      data_lines << coldstart_lines[index]
      index += 1
    end

    data_lines.map! { |line| line.split(DELIMITER) }
    return data_lines
  end

  def generate_table(data, title, headings)
    # Some method/assembly names are huge, so we wrap them to a maximum number
    # of characters per line, so that we can still get an easily readable and
    # pretty printed table with the data.
    data.map! { |m| m.map { |str| wrap_string(str, 80) }.to_a }

    table = Terminal::Table.new do |t|
      t.title = title
      t.headings = headings
      t.rows = data
      t.style = {:width => 180,
                 :padding_left => 1,
                 :border_x => '-',
                 :all_separators => true}
    end

    print("\n#{table}\n")
  end
end
