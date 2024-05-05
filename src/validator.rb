# File: validator.rb

require_relative 'utils'

class Validator

  def self.validate_all(context)
    print_banner('Quick Initial Validation!')
    is_ready = true

    context.stages.each do |stage|
      case stage
      when 'analyze-trace'
        is_ready = self.validate_analyze_trace(context.analyzetrace_context)
      when 'build-worker'
        is_ready = self.validate_build_worker(context.buildworker_context)
      when 'run-benchmarks'
        is_ready = self.validate_run_benchmarks(context.runbenchmarks_context)
      when 'patch-sdk'
        is_ready = self.validate_patch_sdk(context.patchsdk_context)
      end
    end
    return is_ready
  end

  def self.validate_analyze_trace(at_ctx)
    print("\nValidating 'analyze_trace' params...\n")
    ready = true

    # TODO: There is a bit of code duplication here that we might be able to
    #       refactor and clean up a bit.

    if (at_ctx.traces.any? { |t| File.extname(t) != '.coldstart' } and
        !ENV['OS'].casecmp?('Windows_NT'))
      print("Apologies, but unprocessed traces were found and the analyzer" \
            " tools currently only run on Windows :(\n")
      return false
    end

    if (at_ctx.traces.empty?)
      print("No traces or coldstart files were passed, so we can't perform" \
            " analysis :(\n")
      ready = false
    end

    at_ctx.traces.each do |tfile|
      unless File.exist?(tfile)
        print("Trace/Coldstart file #{tfile} was not found :(\n")
        ready = false
      end
    end

    if (at_ctx.traces.any? { |t| File.extname(t) != '.coldstart' })
      if (at_ctx.analyzerapp.nil?)
        print("There are some unprocessed traces but no analyzer app passed :(\n")
        ready = false
      elsif !File.exist?(analyzerapp)
        print("Analyzer App #{at_ctx.analyzerapp} was not found :(\n")
        ready = false
      end
    end

    if (at_ctx.metric.nil?)
      print("No metric to analyze was passed. Setting it to 'jit' as default.\n")
      at_ctx.metric = 'jit'
    end

    unless ['jit', 'worker-jit', 'worker-asm-loader'].include?(at_ctx.metric)
      print("The given metric #{at_ctx.metric} was not recognized :(\n")
      print("The possible values are: jit, worker-jit, and worker-asm-loader.\n")
      ready = false
    end
    return ready
  end

  def self.validate_build_worker(bw_ctx)
    print("\nValidating 'build_worker' params...\n")
    ready = true

    unless ENV['OS'].casecmp?('Windows_NT')
      print("Apologies, but while the worker technically supports all platforms," \
            " our current script hasn't implemented that support yet :(\n")
      return false
    end

    # If both, '--build-only' and '--copy-only', flags were passed, then the
    # build-worker stage is instructed to not run anyways later, so we can
    # skip the validations without any risks or issues.
    return true if bw_ctx.buildonly and bw_ctx.copyonly

    # We're adding the "or not the other condition" here, to also account for
    # when neither flag is passed, which means we will run both.

    if (bw_ctx.buildonly or not bw_ctx.copyonly)
      unless Dir.exist?(bw_ctx.azfunctions_repo)
        print("The Azure Functions repo #{bw_ctx.azfunctions_repo} was not found :(\n")
        ready = false
      end

      unless Dir.exist?(bw_ctx.sdk)
        print("The SDK path #{bw_ctx.sdk} was not found :(\n")
        ready = false
      end
    end

    if (bw_ctx.copyonly or not bw_ctx.buildonly)
      if (bw_ctx.workpath.nil?)
        print("Work path to copy the artifacts to was not provided :(\n")
        ready = false
      end
    end
    return ready
  end

  def self.validate_run_benchmarks(rb_ctx)
    print("\nValidating 'run_benchmarks' params...\n")
    ready = true

    unless ENV['OS'].casecmp?('Windows_NT')
      print("Apologies, but the benchmarks running is currently only supported" \
            " on Windows :(\n")
      return false
    end

    unless Dir.exist?(rb_ctx.func_harness_path)
      print("The func-harness work folder #{rb_ctx.func_harness_path} was not found :(\n")
      ready = false
    end

    unless File.exist?(rb_ctx.perfview)
      print("The PerfView executable #{rb_ctx.perfview} was not found :(\n")
      ready = false
    end

    if (rb_ctx.scenario.nil?)
      print("Benchmarking scenario was not provided. Setting it to 'base' as default.\n")
      rb_ctx.scenario = 'base'
    end

    unless (rb_ctx.scenario == 'base' or rb_ctx.scenario == 'prejit')
      print("The given scenario #{rb_ctx.scenario} was not recognized :(\n")
      ready = false
    end

    if (rb_ctx.trace.nil?)
      print("No result trace was provided. Setting it to a default" \
            " named PerfViewData.etl in the func-harness work folder.\n")
      ctx.trace = ctx.func_harness_path.chomp("/").chomp("\\") + "/PerfViewData.etl"
    end
    return ready
  end

  def self.validate_patch_sdk(ps_ctx)
    print("\nValidating 'patch_sdk' params...\n")
    ready = true

    if (ps_ctx.arch.nil?)
      print("No architecture was provided to search the repo :(\n")
      ready = false
    elsif !(ps_ctx.arch == 'x64' or ps_ctx.arch == 'arm64')
      print("The given architecture #{ps_ctx.arch} was not recognized :(\n")
      print("The supported values are: x64 and arm64.\n")
      ready = false
    end

    if (ps_ctx.config.nil?)
      print("No configuration was provided to search the repo :(\n")
      ready = false
    elsif !(ps_ctx.config == 'Release' or ps_ctx.config == 'Debug')
      print("The given configuration #{ps_ctx.config} was not recognized :(\n")
      print("The supported values are: Release and Debug.\n")
      ready = false
    end

    if (ps_ctx.os.nil?)
      print("No operating system was provided to search the repo :(\n")
      ready = false
    elsif !['windows', 'osx', 'linux'].include?(ps_ctx.os)
      print("The given operating system #{ps_ctx.os} was not recognized :(\n")
      print("The supported values are: windows, osx, and linux.\n")
      ready = false
    end

    unless Dir.exist?(ps_ctx.runtime_repo)
      print("The runtime repo path #{ps_ctx.runtime_repo} was not found :(\n")
      ready = false
    end
    return ready
  end

end
