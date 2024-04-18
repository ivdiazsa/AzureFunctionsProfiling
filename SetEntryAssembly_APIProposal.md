# Background and Motivation

As of recent, Azure Functions has been investing heavily on improving performance,
namely startup perf. As part of these efforts, they are looking into being able to
replace the entry assembly at runtime.

The main motivation behind this is to keep the runtime "warm", so that when a customer
app is received, it can start without paying the full cost of startup, and thus
improving overall performance.

This would be achieved by loading the runtime through a *Startup Hook*, and keep it
there spinning. This is called *"Placeholder Mode"*. When an app request is received,
the entry assembly would be swapped from the placeholder app to the customer app and
then execute it in that Azure Functions container.

# Proposed API

This new API would be implemented using native code, directly in System.Private.CoreLib,
with a managed interface the customers would use.

The customers would call this one:
(`src/libraries/System.Private.CoreLib/src/System/Reflection/Assembly.cs`)

```csharp
namespace System.Reflection
{
    public abstract partial class Assembly ...
    {
        ...

        public static void SetEntryAssembly(Assembly newEntryAssembly)
        {
            SetEntryAssemblyInternal(newEntryAssembly);
        }
    }
}
```

Natively in the CoreCLR side, we would have the implementation written using
a **QCall**:
(`src/coreclr/System.Private.CoreLib/src/System/Reflection/Assembly.CoreCLR.cs`)

```csharp
namespace System.Reflection
{
    public abstract partial class Assembly ...
    {
        ...

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "AssemblyNative_SetEntryAssembly")]
        private static partial void SetEntryAssemblyNative(QCallAssembly assembly);

        private static void SetEntryAssemblyInternal(Assembly newEntryAssembly)
        {
            RuntimeAssembly newEntryRAssembly = (RuntimeAssembly) newEntryAssembly;
            SetEntryAssemblyNative(new QCallAssembly(ref newEntryRAssembly));
        }
    }
}
```

And the actual implementation directly in native code:
(`src/coreclr/vm/assemblynative.cpp`)

```c++
extern "C" void QCALLTYPE AssemblyNative_SetEntryAssembly(QCall::AssemblyHandle assemblyHandle)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    printf("STARTED SETTING NEW ENTRY ASSEMBLY!\n");
    Assembly* pAssembly = assemblyHandle->GetAssembly();
    PTR_AppDomain pCurDomain = GetAppDomain();
    pCurDomain->SetRootAssembly(pAssembly);
    printf("FINISHED SETTING NEW ENTRY ASSEMBLY!\n");

    END_QCALL;
}
```

The reason we are opting to go this path is because of the more flexible and easy
communication directly with the runtime. This said, as explained above, the customer
would only have access to the managed interface.

# Usage Examples

The main use case would be Azure Functions containers. They would have a placeholder
app that would actually not get run per se, because there would be a *Startup Hook*
keeping the worker, and therefore the runtime, alive while the customer app arrives.
Once this happens, the worker would take the customer app dll through the startup
hook swapping the placeholder assembly with the customer one. And then, it would run
it without paying the full startup cost up front.

```csharp
internal class StartupHook
{
    static readonly EventWaitHandle WaitHandle = new EventWaitHandle(
        false,
        EventResetMode.ManualReset,
        "AzureFunctionsNetHostSpecializationWaitHandle"
    );

    ...

    public static void Initialize()
    {
        // Initialize the worker that will run the app.
        WorkerEventSource.Log.StartupHookInit();
        ...

        // Await the customer app.
        WorkerEventSource.Log.StartupHookWaitForSpecializationRequestStart();
        WaitHandle.WaitOne();

        // Set the entry assembly to the customer app and continue by executing it.
        Assembly customerAsm = Assembly.LoadFrom("/path/to/customer/app/dll");
        Assembly.SetEntryAssembly(customerAsm);
        WorkerEventSource.Log.StartupHookReceivedContinueExecutionSignalFromFunctionsNetHost();
    }
}
```

A very simple way to see this new API in action would be with an example app like
the one described below:

- First write and build the app that will be run. Let's call it `DummyApp.dll`.
- Then, write a small *Startup Hook* like the one in the code below.
- And lastly, set it to the `DOTNET_STARTUP_HOOKS` environment variable.

Now, whenever you try running any dotnet app, you'll witness the API in action
because the `DummyApp.dll` will always get executed instead.

```csharp
using System;
using System.Reflection;

internal class StartupHook
{
    public static void Initialize()
    {
        string dummyAppPath = "/path/to/DummyApp.dll";
        Assembly overridingAsm = Assembly.LoadFrom(dummyAppPath);
        Assembly.SetEntryAssembly(overridingAsm);
        Console.WriteLine($"Entry Assembly Overridden To '{overridingAsm.FullName}'");
    }
}
```

# Risks

Low, since this is a very specialized API that otherwise doesn't interfere with
any of the runtime's current functionality.

# Benefits

The main benefit of adding this API would be to provide users with an easy way
to run dotnet apps without having to pay the entire cost of startup, as well as
provide more flexibility to systems that might require to run "arbitrary" apps
and assemblies, like in this case the Azure Functions workers.

# Other Notes

/* Other notes go here. */
