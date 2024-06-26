#!/usr/bin/ruby

# File: azure_functions.rb

require_relative 'src/benchmark_runner'
require_relative 'src/command_line_parser'
require_relative 'src/functions_nethost_manager'
require_relative 'src/sdk_patcher'
require_relative 'src/trace_analyzer'
require_relative 'src/validator'
require_relative 'src/utils'

print_banner("AZURE FUNCTIONS!")
context = CommandLineParser.parse_into_context(ARGV)

# Run each of the desired stages. Currently, this is just symbolic since only one
# stage at a time is supported, but we're laying the groundwork and basis for the
# next steps since now :)

unless Validator.validate_all(context)
  print("\nUnfortunately, the provided parameters, paths, etc had issues that" \
        " prevent the Azure Functions script from functioning correctly :(\n\n")
  exit -1
end

context.stages.each do |stage|
  case stage
  when 'analyze-trace'
    ctx = context.analyzetrace_context

    analyzer = TraceAnalyzer.new(
      ctx.analyzerapp,
      ctx.tracepaths
    )

    analysis_kind = ctx.comparetype.nil? ?
                      'table-display'    :
                      "compare-#{ctx.comparetype}"

    analyzer.run(analysis_kind, ctx.metric)

  when 'build-worker'
    ctx = context.buildworker_context

    azure_manager = FunctionsNetHostManager.new(
      ctx.workpath,
      ctx.azfunctions_repo,
      ctx.sdk)

    azure_manager.build_repo() unless ctx.copyonly
    azure_manager.copy_artifacts_to_work_zone() unless ctx.buildonly

    if (ctx.buildonly and ctx.copyonly)
      print("\nThe flags build-only and copy-only are mutually exclusive." \
            " Will skip the 'build-worker' stage in this run.\n")
      next
    end

  when 'run-benchmarks'
    ctx = context.runbenchmarks_context

    benchmarker = BenchmarkRunner.new(
      ctx.func_harness_path,
      ctx.perfview,
      ctx.scenario,
      ctx.trace
    )

    benchmarker.run()

  when 'patch-sdk'
    ctx = context.patchsdk_context

    patcher = SdkPatcher.new(
      ctx.arch,
      ctx.config,
      ctx.os,
      ctx.runtime_repo,
      ctx.sdkchannel,
      ctx.workpath
    )

    patcher.downloadx_nightly_sdk(ctx.redownload)
    patcher.patch_sdk()

  else
    raise "Got an unexpected stage '#{stage}' :("
  end
end

print_banner("DONE!")
