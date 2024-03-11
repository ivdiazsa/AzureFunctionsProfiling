# Azure Functions Profiling

Little repo to keep all my stuff about the Azure Functions investigations.

## The Main Script

The little script named `AzureFunctionsProfiling.ps1` contains all the functionality
for gathering traces and analyzing them. It has two main components: The runner
and the analyzer. By default, it will run both, but it can also be instructed to
only run one of them as needed. Each of these components is explained in the
following sections, as well as their respective instructions.

It is important to mention that the little script requires _Administrator privileges_
to run, so make sure your terminal is launched in such way. The reason for this
requirement is that PerfView needs the elevation to work correctly.

### The Run Stage

This stage is in charge of running the `func-harness` tool with the example app,
and capturing a trace with _PerfView_.

**NOTE**: Currently, PerfView still launches an external window despite me using the
`-NoWindow` flag, which requires us to click once it's finished. I'm looking for a
way to get around this. However, even with this slight annoyance, it still makes the
process of running and getting the trace much faster than having to manually click
all the options in PerfView and the harness tool.

### The Analyze Stage

This stage is in charge of passing the generated or provided trace to the analyzer
tool. Currently, it will print the whole output just like if the analyzer tool was
run on its own, but I'm already working on implementing filtering and processing
of the information and the data.

### How to Use the Script

Now, as for the instructions on how to use the little script. The process is as
simple as opening an elevated Powershell prompt (this has been tested and confirmed
to work with Command Prompt, Powershell, and Windows Terminal), and running the
script with the necessary parameters.

All parameters are optional, in the sense that they all have a default value set,
but make sure to modify them in the command-line as necessary to fit your own
directory tree structure.

The supported parameters are the following:

* `-appHostDir` (Default: _Current Directory_):
  This param contains the path to the folder containing the artifacts used by
  `func-harness`. This is where `harness.settings.json`, `FunctionApp44`, and
  so on are located.

* `-analyzerName` (Default: `FunctionsColdStartProfileAnalyzer.exe`)
  This param contains the name of the analyzer executable. Currently, there is no
  need to change the default value because the Azure Functions folks are providing
  us with the tool, but I added it just in case.

* `-analyzerPath` (Default: _Current Directory_)
  This param contains the path to the folder that contains the tool denoted by the
  previous param `-analyzerName`. If omitted, the script will look for it in the
  current working directory.

* `-mode` (Default: `all`):
  This param indicates the script which stage(s) to run. By default, it is set
  to `all`, which means it will run the benchmark and then process the resulting
  trace of said run. The possible values are `run`, `analyze`, and `all`.

* `perfviewExePath` (Default: `$appHostDir/PerfView.exe`):
  This param contains the full path to the PerfView exe to use during the run.
  If you have your PerfView executable in the same folder as the function app
  host, then you can omit this parameter.

* `-scenario` (Default: `base`):
  Currently, the Azure Functions folks have provided us with 4 variants of the
  test function app. This param indicates the script with one to run. The possible
  values are `base`, `prejit`, `preload`, and `preload+prejit`.

* `-traceName` (Default: `PerfViewData.etl`):
  This param contains the name of the resulting trace file/trace file to analyze.
  It can be omitted if the value passed to `-tracePath` includes the filename.
  See the note below for an example illustrating this.

* `-tracePath` (Default: _Current Directory_):
  This param contains the path where we want the traces to be stored/searched. Note
  that, as specified above, you can pass either the containing directory, or the
  full path including the filename. Like for example, the following calls are
  equivalent:

  ```powershell
  ./AzureFunctionsProfiling.ps1 -traceName MyTrace.etl -tracePath C:/Path/To/Traces
  ```

  ```powershell
  ./AzureFunctionsProfiling.ps1 -tracePath C:/Path/To/Traces/MyTrace.etl
  ```

  Whichever option you use is supported by the script.

#### Examples

Do the full run (run and analyze) where the Functions App Host is saved to a directory
in `C:/Dev/FunctionsAppHost`, running in `prejit` mode, using a PerfView executable in
that same folder. The analyzer tool is saved to `C:/Dev/ProfileAnalyzer`.

```powershell
./AzureFunctionsProfiling.ps1 -appHostDir C:/Dev/FunctionsAppHost `
                              -scenario prejit `
                              -analyzerPath C:/Dev/ProfileAnalyzer
```

Just capture a trace to analyze elsewhere. In this case, we're using a preinstalled
PerfView executable in the running machine. The `base` scenario is run here.

```powershell
./AzureFunctionsProfiling.ps1 -appHostDir C:/Dev/FunctionsAppHost `
                              -perfviewExePath C:/Program Files/PerfView/PerfView.exe
```

Just run the profiler on a trace we got elsewhere and is saved to C:/Dev/Traces.

```powershell
./AzureFunctionsProfiling.ps1 -analyzerPath C:/Dev/ProfileAnalyzer `
                              -tracePath C:/Dev/Traces `
                              -traceName MyAcquiredFromElsewhereTrace.etl
```
