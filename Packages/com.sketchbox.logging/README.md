# com.sketchbox.logging

Logging facade and global exception handler.

## Purpose

Call sites use `Sketchbox.Logging.Log` instead of `UnityEngine.Debug`. Severity
filtering, channel tagging and output routing are then configured in one place
rather than at each call site.

Default behaviour matches `UnityEngine.Debug`: output goes to the Unity console,
with the context object preserved so console entries remain click-to-select.

## Usage

```csharp
using Log = Sketchbox.Logging.Log;

Log.Info("Scenario loaded", this);
Log.Warn("Falling back to default profile");
Log.Error("Asset bundle missing: " + name);
Log.Exception(exception);
```

Channel-scoped logging:

```csharp
static readonly ChannelLog Net = Log.Channel("net");
Net.Info("Peer connected");
```

## Severity

`Log.MinimumLevel` discards messages below the given level before any string
formatting runs. It defaults to `Debug` in the editor and in development builds,
and to `Info` otherwise.

`Log.Trace` and `Log.TraceFormat` are marked `[Conditional]`, so those calls and
their argument expressions are removed by the compiler in release player builds.

## Routing output elsewhere

Implement `ILogSink` and install it:

```csharp
Log.SetSink(new MySink());
```

Passing `null` restores the Unity console sink.

## Global exception handler

`GlobalExceptionHandler` installs automatically via `RuntimeInitializeOnLoadMethod`
before the first scene loads. It reports:

- uncaught exceptions on the Unity player loop
- uncaught exceptions on background threads
- faulted `Task` objects whose exception is never observed, where the scripting
  runtime provides `System.Threading.Tasks`

Handlers report only. They do not suppress exceptions, so crash behaviour is
unchanged. Subscribe to `GlobalExceptionHandler.UnhandledFailure` to forward
failures to crash reporting.

## Scripting runtime

The package targets Unity 2018.3 and later. The unobserved-`Task` handler is
compiled only when `NET_4_6` or `NET_STANDARD_2_0` is defined; on the legacy
.NET 3.5 runtime the remaining two handlers still install.
