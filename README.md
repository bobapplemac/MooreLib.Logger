# MooreLib.Logger

A small application-focused logging layer for .NET that combines **NLog-backed structured logging** with the progressive, console-style output patterns that are awkward to express with traditional one-event-per-line logging APIs.

MooreLib.Logger keeps the common case simple:

```csharp
Log.Info("Application starting.");
Log.Warn("Configuration value is deprecated.");
Log.Error("Unable to connect to the server.", exception);
```

but also supports logical multi-line entries and true incremental writes:

```csharp
using var entry = Log.BeginInlineInfo("Connecting to PLC - ");
Log.Write("CONNECTED - ");
Log.Write("PROGRAM: Main - ");
Log.CompleteEntry("SUCCESS");
```

which can render as a single progressively written physical line:

```text
2026-08-25 19:00:00 [INFO] - Connecting to PLC - CONNECTED - PROGRAM: Main - SUCCESS
```

The same physical fragments are written immediately to both console and file destinations. MooreLib.Logger is therefore useful when converting console-oriented applications to persistent logging without giving up natural `Write(...)` / `WriteLine(...)` style progress output.

> **Current source version:** 1.13.0 (revision r13)  
> **Target framework:** .NET 8  
> **Logging backend:** NLog 6.2  
> **Public namespace:** `MooreLib.Logging`  
> **License:** MIT

## Contents

- [Why MooreLib.Logger?](#why-mooreliblogger)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Core usage](#core-usage)
- [Nested entries](#nested-entries)
- [Structured properties](#structured-properties)
- [Async and explicit entry context](#async-and-explicit-entry-context)
- [Inline interruption and resume](#inline-interruption-and-resume)
- [Blank physical lines](#blank-physical-lines)
- [Multiline message semantics](#multiline-message-semantics)
- [File logging](#file-logging)
- [File rollover and retention](#file-rollover-and-retention)
- [Filtering and routing](#filtering-and-routing)
- [Configuration reference](#configuration-reference)
- [Custom NLog layouts](#custom-nlog-layouts)
- [Lifetime and disposal](#lifetime-and-disposal)
- [Concurrency model](#concurrency-model)
- [Important behavioral rules](#important-behavioral-rules)
- [API overview](#api-overview)
- [Project structure](#project-structure)
- [Building](#building)
- [Tests](#tests)
- [Design scope](#design-scope)
- [Thread-safety and unusual ordering](#thread-safety-and-unusual-ordering)
- [Versioning](#versioning)
- [License](#license)
- [Status](#status)

---

## Why MooreLib.Logger?

Traditional logging libraries are intentionally event-oriented: one call creates one complete log event. That model works extremely well for most applications, and MooreLib.Logger uses it for ordinary `Trace`, `Debug`, `Info`, `Warn`, `Error`, and `Fatal` events.

Some applications, however, naturally produce output incrementally:

```csharp
Console.Write("Connecting... ");
Connect();
Console.Write("CONNECTED - ");
ReadIdentity();
Console.WriteLine("SUCCESS");
```

Retrofitting conventional logging often forces that output to be rewritten as several unrelated lines or manually buffered into a final message.

MooreLib.Logger adds a semantic layer above NLog for this use case. It provides:

- ordinary one-shot severity logging;
- logical multi-line entries;
- incremental partial-line `Write(...)` output;
- `WriteLine(...)` continuation lines;
- nested parent/child entries with tree-style rendering;
- automatic interruption and resume of open inline lines;
- `AsyncLocal` ambient entry context across `async` / `await`;
- explicit entry references for cross-context work;
- deterministic `LogEntry : IDisposable` handles;
- structured `LogProperty` metadata and inheritance;
- exception-aware multi-line rendering while preserving the actual `Exception` object in NLog;
- independently configurable console and file severity thresholds;
- dynamically enabled/disabled file logging;
- live partial-line file output;
- safe-boundary file rollover that avoids splitting a physical line across archive files.

MooreLib.Logger is **not intended to replace NLog**. NLog remains responsible for targets, file I/O, archive mechanics, retention, layouts, event transport, and backend exception support. MooreLib supplies application-level semantics that NLog does not inherently know about, particularly logical entries and progressive physical-line output.

---

## Requirements

- .NET 8.0 or later
- NLog 6.2.0 or later

The project currently references:

```xml
<PackageReference Include="NLog" Version="6.2.0" />
```

---

## Installation

A NuGet package is not currently published. The planned package ID is `MooreLib.Logger`.

For now, clone the repository and reference the library project directly from your application or solution:

```bash
git clone https://github.com/bobapplemac/MooreLib.Logger.git
```

Example project reference when the consuming project is a sibling of the library project:

```xml
<ItemGroup>
  <ProjectReference Include="../MooreLib.Logger/MooreLib.Logger.csproj" />
</ItemGroup>
```

The package/project identity is `MooreLib.Logger`, while the public API lives in the `MooreLib.Logging` namespace. This avoids the awkward fully-qualified type name `MooreLib.Logger.Logger` while keeping the repository, assembly, project, and eventual NuGet package names aligned.

Repository: [github.com/bobapplemac/MooreLib.Logger](https://github.com/bobapplemac/MooreLib.Logger)

---

## Quick start

```csharp
using MooreLib.Logging;

using var Log = new Logger(new LoggerOptions
{
    IncludeConsoleTimestamp = true,
    IncludeConsoleLogLevel = true,
    IncludeFileTimestamp = true,
    IncludeFileLogLevel = true,
    TimestampFormat = "yyyy-MM-dd HH:mm:ss",
    TimestampZone = LogTimestampZone.Local,
    MessageSeparator = " - ",
    MinimumConsoleLevel = LogLevel.Debug,
    MinimumFileLevel = LogLevel.Trace,
    ArchivePolicy = new FileArchivePolicy.BySize(
        MaximumFileSizeBytes: 10 * 1024 * 1024,
        MaximumArchiveFiles: 5)
});

Log.EnableFileLogging("Application.log");

Log.Info("Application starting.");
Log.Debug("Configuration loaded.");
```

A typical line might look like:

```text
2026-08-25 19:00:00 [INFO] - Application starting.
```

The exact appearance depends on `LoggerOptions` and any custom NLog layouts supplied.

---

# Core usage

## One-shot logging

The familiar severity methods are available directly:

```csharp
Log.Trace("Detailed protocol state.");
Log.Debug("Configuration loaded.");
Log.Info("Service started.");
Log.Warn("Retrying connection.");
Log.Error("Operation failed.");
Log.Fatal("Application cannot continue.");
```

For dynamic severity selection:

```csharp
Log.WriteEvent(LogLevel.Info, "Service started.");
```

### Exceptions

Exception overloads use **message first, exception second**:

```csharp
try
{
    Connect();
}
catch (Exception exception)
{
    Log.Error("Unable to connect to PLC.", exception);
}
```

MooreLib renders the human-readable exception as physical continuation lines while keeping the original `Exception` object attached to the underlying NLog event for structured/custom targets.

Example:

```text
2026-08-25 19:00:00 [ERROR] - Unable to connect to PLC.
├ System.Net.Sockets.SocketException (10061): Connection refused...
├    at System.Net.Sockets.Socket.Connect(...)
└    at Application.Connect(...)
```

---

## Logical multi-line entries

A logical entry groups several physical lines while keeping them visually related:

```csharp
using var entry = Log.BeginInfo("Deploying application.");

Log.WriteLine("Configuration validated.");
Log.WriteLine("Package downloaded.");
Log.CompleteEntry("Deployment complete.");
```

Example output:

```text
2026-08-25 19:00:00 [INFO] - Deploying application.
├ Configuration validated.
├ Package downloaded.
└ Deployment complete.
```

`Begin...()` returns a `LogEntry` handle. The handle implements `IDisposable`, so an early return or exception can still deterministically clean up an entry:

```csharp
using var entry = Log.BeginInfo("Processing request.");

if (!Validate())
{
    Log.CompleteEntry("Validation failed.");
    return;
}

Log.CompleteEntry("Complete.");
```

Disposing an already completed entry is harmless.

---

## Progressive inline output

Progressive output is one of the primary reasons MooreLib.Logger exists.

```csharp
using var entry = Log.BeginInlineInfo("Connecting to PLC - ");

Log.Write("CONNECTED - ");
Log.Write("NAME: PLC01 - ");
Log.Write("PROGRAM: Main - ");
Log.CompleteEntry("SUCCESS");
```

The physical line remains open between calls:

```text
2026-08-25 19:00:00 [INFO] - Connecting to PLC - CONNECTED - NAME: PLC01 - PROGRAM: Main - SUCCESS
```

Fragments are emitted immediately. When file logging is enabled, they are written and flushed to the active log file as they occur rather than being buffered until `CompleteEntry()`.

### `Write(...)` vs. `WriteLine(...)`

`Write(...)` appends to the current physical line without terminating it:

```csharp
Log.Write("25% ");
Log.Write("50% ");
Log.Write("75% ");
```

`WriteLine(...)` writes within the current logical entry and terminates the current physical line while leaving the logical entry active:

```csharp
Log.WriteLine("Configuration validated.");
Log.WriteLine("Package downloaded.");
```

`CompleteEntry(...)` completes the logical entry and, when appropriate, emits its terminal `└` line.

---

## Mixed inline and multi-line entries

A single logical entry can freely mix line-oriented and partial-line output:

```csharp
using var entry = Log.BeginInfo("Processing package.");

Log.WriteLine("Metadata validated.");
Log.Write("Downloading: ");
Log.Write("25% ");
Log.Write("50% ");
Log.WriteLine("100%");
Log.CompleteEntry("Processing complete.");
```

Example output:

```text
2026-08-25 19:00:00 [INFO] - Processing package.
├ Metadata validated.
├ Downloading: 25% 50% 100%
└ Processing complete.
```

---

## Incrementally constructing the terminal line

`CompleteEntryInline(...)` begins the terminal `└` line but leaves that physical line open:

```csharp
using var entry = Log.BeginInfo("Running validation.");

Log.WriteLine("Checks complete.");
Log.CompleteEntryInline("Result: ");
Log.Write("SUCCESS");
Log.CompleteEntry();
```

Example:

```text
2026-08-25 19:00:00 [INFO] - Running validation.
├ Checks complete.
└ Result: SUCCESS
```

Once an entry has entered terminal-inline completion, it is committed to completion. Continue the final line with `Write(...)`, then call `CompleteEntry()`.

---

# Nested entries

Logical entries can be nested beneath an active parent.

```csharp
using var parent = Log.BeginInfo("Updating PLC clock.");

Log.WriteLine("Connected.");

using var child = Log.BeginInfo(parent, "Reading controller identity.");
Log.WriteLine("Product: ControlLogix");
Log.CompleteEntry("Identity complete.");

Log.CompleteEntry(parent, "Clock update complete.");
```

Nested entries retain their own ID, parent ID, depth, and inherited structured properties.

A child that has already been created may continue after its parent is completed. MooreLib intentionally uses **loose parent/child lifetime semantics**: valid requested output is preferred over enforcing strict LIFO tree lifetimes. A completed parent, however, cannot accept a newly created child.

---

## Parent-aware one-shot events

A child event can also be attached without creating a child entry that must later be completed:

```csharp
using var entry = Log.BeginInfo("Deploying application.");

Log.Info(entry, "Package validated.");
Log.Warn(entry, "Fallback mirror selected.");
Log.CompleteEntry("Deployment complete.");
```

Exception-bearing attached events are supported as well:

```csharp
Log.Error(entry, "PLC communication error.", exception);
```

---

# Structured properties

Structured properties are a normal public capability:

```csharp
Log.Info(
    "Connected to PLC.",
    new LogProperty("Address", "192.168.10.50"),
    new LogProperty("Program", "MainProgram"));
```

Properties may also be attached to logical entries:

```csharp
using var entry = Log.BeginInfo(
    "Deploying package.",
    new LogProperty("Package", "Runtime"),
    new LogProperty("Version", "2.4.1"));
```

Child entries inherit their parent's properties. A property supplied by the child overrides an inherited value with the same name.

MooreLib reserves the `MooreLib.Logger.*` property namespace for internal entry metadata. Caller-supplied properties in that namespace are rejected rather than silently overwritten.

Reserved metadata includes information equivalent to:

- entry ID;
- parent entry ID;
- entry event type;
- entry depth.

The metadata remains attached to NLog events even when it is hidden from the human-readable file layout.

Because application properties and MooreLib metadata are attached to the underlying NLog event, they can also be consumed by structured NLog layouts/targets. This makes the same logging calls suitable for both human-readable console/file output and machine-readable formats such as JSON without encoding operational data into the message text itself.

MooreLib.Logger does not implement a remote logging database or transport layer. If an application later forwards structured events to a document store, telemetry system, or other backend, that integration belongs at the NLog/collector layer rather than in the MooreLib logging API.

---

# Async and explicit entry context

The current logical entry is tracked with `AsyncLocal`, which follows .NET `ExecutionContext` rather than a specific physical thread. Ambient entry context therefore normally flows through `async` / `await`, `Task.Run(...)`, and ordinary `Thread` execution-context flow:

```csharp
using var entry = Log.BeginInfo("Processing request.");

await DoWorkAsync();

Log.WriteLine("Async work complete.");
Log.CompleteEntry("Done.");
```

For work that must target a specific entry explicitly, use the returned `LogEntry` handle or its numeric ID:

```csharp
using var entry = Log.BeginInfo("Processing.");

Log.WriteLine(entry, "Explicitly attached line.");
```

`LogEntry` has an implicit conversion to `long`, so APIs accepting an explicit entry ID can use the handle directly.

Concurrent execution flows can establish their own child entries and then continue using implicit `Write(...)` / `WriteLine(...)` calls without passing an ID on every call. Each `ExecutionContext` retains its own ambient entry. Code that deliberately suppresses `ExecutionContext` flow, or otherwise crosses a boundary where ambient context should not be assumed, should use the explicit `LogEntry`/ID overloads.

Explicit entry references are intentionally strict. An invalid or completed explicit ID is a programming error and throws rather than silently becoming an unrelated standalone log event.

Ambient operations are more forgiving where documented; for example, line-oriented operations may fall back to standalone output when no ambient entry exists.

---

# Inline interruption and resume

Only one logical entry can own the currently open physical output line.

If another visible event needs to write while an inline entry owns an open line, MooreLib terminates that line, emits the competing event, and marks the original entry as interrupted. The next write to the interrupted entry resumes on a new physical line using `InlineResumePrefix`.

Example:

```csharp
using var entry = Log.BeginInlineInfo("Downloading - ");
Log.Write("25% ");

Log.Warn("Network latency detected.");

Log.Write("50% ");
Log.CompleteEntry("100%");
```

Conceptually:

```text
2026-08-25 19:00:00 [INFO] - Downloading - 25%
2026-08-25 19:00:01 [WARN] - Network latency detected.
↳ 50% 100%
```

A completely filtered event does **not** interrupt an open line. If an event is visible on at least one enabled destination, however, it participates in the shared physical stream even if another destination filters that event.

---

# Blank physical lines

`WriteBlankLine()` writes exactly one unformatted blank physical line:

```csharp
Log.WriteBlankLine();
```

This is intentionally a **physical-stream command**, not an `Info` event. It bypasses normal severity thresholds and writes to every enabled destination.

If an inline entry currently owns an open physical line, the blank-line command interrupts it. The entry resumes normally on its next write.

---

# Multiline message semantics

APIs that support physical multiline output recognize CR, LF, and CRLF line separators and preserve intentional blank/trailing physical lines.

Representative behavior:

```text
""       -> [""]
"a"      -> ["a"]
"a\n"    -> ["a", ""]
"a\nb"   -> ["a", "b"]
"a\n\n"  -> ["a", "", ""]
"\n"     -> ["", ""]
"\n\n"   -> ["", "", ""]
```

Equivalent CR and CRLF sequences behave the same way.

`Write(...)` is intentionally different because it represents a single open physical line. Embedded newline sequences passed to `Write(...)` are normalized to spaces.

---

# File logging

Console logging is configured when the logger is constructed. File logging is optional and may be enabled later:

```csharp
Log.EnableFileLogging("Application.log");
```

It may be disabled while the logger continues writing to the console:

```csharp
Log.DisableFileLogging();
```

Changing from one path to another is also supported:

```csharp
Log.EnableFileLogging("Application-2.log");
```

Configuration changes are transactional. MooreLib prepares a prospective NLog configuration and destination state before committing it. If configuration fails, the previous working configuration and destination ownership remain intact.

Dynamic destination/filter changes are **prospective**:

> Previously suppressed physical content is not replayed.

A logical entry may remain active while filtered. If a destination later becomes available, only future eligible output is emitted.

---

## Destination ownership

MooreLib enforces process-local destination ownership among participating `Logger` instances.

- only one live MooreLib logger may own the console physical stream;
- only one live MooreLib logger may own a normalized file path;
- ownership is released when file logging is disabled or the logger is disposed.

This guarantee applies **only among MooreLib.Logger instances in the same process**. MooreLib cannot prevent unrelated `Console.WriteLine(...)` calls, another library, or another process from independently writing to the same destination.

The intended architecture is therefore generally **one master `Logger` instance per application**.

---

# File rollover and retention

Two archive policies are available.

## Size-based rollover

```csharp
ArchivePolicy = new FileArchivePolicy.BySize(
    MaximumFileSizeBytes: 10 * 1024 * 1024,
    MaximumArchiveFiles: 5)
```

Defaults:

- maximum active size: 10 MiB;
- retained archives: 5.

`MaximumArchiveFiles` supports:

```text
-1  unlimited / no count restriction
 0  retain zero archived files
 1+ retain that many archives
```

The size threshold is intentionally approximate. If the threshold is crossed while an inline physical line is open, MooreLib allows that line to finish and rolls over at the next safe physical-line boundary.

## Daily rollover

```csharp
ArchivePolicy = new FileArchivePolicy.Daily(
    MaximumArchiveDays: 14)
```

If the date changes while a physical line is open, that line is allowed to finish in the current file. Rollover occurs before the next eligible physical line.

### Physical lines are atomic; logical entries are not

A single physical line should not be divided across two archive files.

A multi-line logical entry **may** span archive files:

```text
old file:
Operation
├ Step 1
├ Step 2

new file:
├ Step 3
└ Complete
```

This prevents long-lived logical entries from indefinitely blocking rollover.

---

## Following a live file across rollover

Partial-line file fragments are written immediately and the configured active log path remains the active filename.

NLog's rename/recreate archive behavior can replace the underlying file/inode during rollover. On Unix-like systems, a long-running plain:

```bash
tail -f Application.log
```

may continue following the renamed archived file.

For rotation-aware following, prefer:

```bash
tail -F Application.log
```

or an equivalent viewer that follows by filename and retries after replacement.

---

# Filtering and routing

`LoggerOptions` provides independent minimum levels for console and file output:

```csharp
MinimumConsoleLevel = LogLevel.Info,
MinimumFileLevel = LogLevel.Trace,
```

This allows, for example, concise interactive console output while retaining detailed diagnostics in the file.

Console routing is split by severity:

- `Trace` through `Warning` -> standard output, subject to `MinimumConsoleLevel`;
- `Error` and `Fatal` -> standard error, subject to `MinimumConsoleLevel`.

File events are emitted when they meet `MinimumFileLevel`.

A suppressed event that is invisible to **all enabled destinations** has no physical-stream effect: it does not interrupt an open inline line or trigger resume behavior.

---

# Configuration reference

## `LoggerOptions`

| Option | Default | Description |
|---|---:|---|
| `LoggerName` | `"MooreLib.Logger"` | NLog logger name used by this instance. |
| `MinimumConsoleLevel` | `Debug` | Minimum severity emitted to the console. |
| `MinimumFileLevel` | `Debug` | Minimum severity emitted to the optional file target. |
| `IncludeConsoleTimestamp` | `false` | Include timestamps in generated console headers. |
| `IncludeConsoleLogLevel` | `true` | Include severity labels in generated console headers. |
| `IncludeFileTimestamp` | `true` | Include timestamps in generated file headers. |
| `IncludeFileLogLevel` | `true` | Include severity labels in generated file headers. |
| `IncludeFileEntryMetadata` | `true` | Render MooreLib entry metadata in the generated file layout. Metadata remains attached even when hidden. |
| `TimestampFormat` | `yyyy-MM-dd HH:mm:ss` | .NET date/time format used by generated layouts. Validated during construction. |
| `TimestampZone` | `Local` | `Local` or `Utc`. |
| `MessageSeparator` | single space | Separator between generated header content and message text. |
| `ConsoleLayout` | `null` | Optional complete NLog layout override for header-bearing console lines. |
| `ConsoleFragmentLayout` | `${message}` | NLog layout used for console fragments that must not repeat the header. |
| `FileLayout` | `null` | Optional complete NLog layout override for header-bearing file lines. |
| `FileFragmentLayout` | `${message}` | NLog layout used for file fragments. |
| `InlineResumePrefix` | `↳ ` | Prefix used when an interrupted inline entry resumes. |
| `EntryIndentSize` | `2` | Spaces added per nested entry depth. |
| `DisposeFlushTimeout` | 5 seconds | Maximum NLog flush wait during deterministic disposal. |
| `ArchivePolicy` | 10 MiB / 5 archives | File archive/retention strategy. |

Example:

```csharp
var options = new LoggerOptions
{
    LoggerName = "MyApplication",
    MinimumConsoleLevel = LogLevel.Info,
    MinimumFileLevel = LogLevel.Debug,

    IncludeConsoleTimestamp = true,
    IncludeConsoleLogLevel = true,
    IncludeFileTimestamp = true,
    IncludeFileLogLevel = true,

    TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff",
    TimestampZone = LogTimestampZone.Local,
    MessageSeparator = " - ",

    InlineResumePrefix = "↳ ",
    EntryIndentSize = 2,

    ArchivePolicy = new FileArchivePolicy.BySize(
        MaximumFileSizeBytes: 25 * 1024 * 1024,
        MaximumArchiveFiles: 10)
};

using var Log = new Logger(options);
```

Options are validated during construction so configuration errors fail early rather than during an unrelated logging operation.

---

# Custom NLog layouts

MooreLib supports complete NLog layout overrides for header-bearing console and file lines:

```csharp
var options = new LoggerOptions
{
    ConsoleLayout = "${longdate} [${level:uppercase=true}] ${message}",
    FileLayout = "${longdate} [${level:uppercase=true}] ${callsite} ${message}"
};
```

MooreLib dispatches events through NLog's wrapper-aware API:

```csharp
_nlogLogger.Log(typeof(Logger), logEvent);
```

so callsite-oriented renderers such as `${callsite}` and `${callsite-linenumber}` can identify the application caller rather than stopping inside the MooreLib wrapper.

Fragment layouts are configured separately because continuation fragments must not automatically repeat a line header:

```csharp
ConsoleFragmentLayout = "${message}",
FileFragmentLayout = "${message}"
```

---

# Lifetime and disposal

Both `Logger` and `LogEntry` implement deterministic lifetime behavior.

```csharp
using var Log = new Logger();
using var entry = Log.BeginInfo("Operation.");
```

`LogEntry.Dispose()` completes/unregisters the entry if it is still active and is idempotent when the entry has already been completed explicitly.

`Logger.Dispose()`:

- terminates an open physical line safely;
- clears active logical state;
- flushes NLog within `DisposeFlushTimeout`;
- releases file ownership;
- releases console ownership;
- disposes the instance-owned NLog `LogFactory`.

Cleanup is designed so destination ownership is released even if an earlier shutdown operation such as flushing fails.

No finalizer attempts to complete logical entries. Finalization cannot safely reconstruct original output ordering or `AsyncLocal` context.

---

# Concurrency model

MooreLib.Logger is designed primarily for one master logger used throughout an application. Mostly synchronous usage is common, but occasional concurrent and asynchronous logging must remain correct.

Physical output is inherently serialized. MooreLib therefore intentionally uses one authoritative coordinator for decisions such as:

- who owns an open physical line;
- whether competing output interrupts it;
- whether a resumed entry receives `InlineResumePrefix`;
- entry lifecycle transitions;
- configuration transitions;
- safe rollover boundaries;
- disposal.

The goal is deterministic physical output and valid state transitions rather than maximizing internal logging concurrency.

---

# Important behavioral rules

The following rules are intentional parts of the public behavior:

1. Explicit entry references must identify an active entry or the operation throws.
2. Ambient context flows through `async` / `await`, but the active-entry registry remains authoritative.
3. A child that already exists may continue after its parent completes.
4. A completed parent cannot accept a new child.
5. Only one entry can own the currently open physical line.
6. Visible competing output permanently interrupts an open physical line.
7. An interrupted entry resumes on a new line when it next writes.
8. An event filtered from every enabled destination does not interrupt visible output.
9. Console and file observe one conceptual physical stream.
10. File fragments are emitted immediately rather than buffered until newline.
11. A physical line is kept together across file rollover whenever reasonably possible.
12. A logical multi-line entry may span archive files.
13. Dynamic destination/filter changes are prospective; suppressed history is never replayed.
14. Structured MooreLib metadata uses the reserved `MooreLib.Logging.*` namespace.
15. Destination exclusivity is process-local and applies only among participating MooreLib logger instances.

---

# API overview

## Severity

```csharp
Trace(...)
Debug(...)
Info(...)
Warn(...)
Error(...)
Fatal(...)
WriteEvent(LogLevel level, ...)
```

All severity methods support structured `LogProperty` values. Exception overloads are available for each severity.

## Begin logical entries

```csharp
BeginEntry(level, message, ...)
BeginTrace(...)
BeginDebug(...)
BeginInfo(...)
BeginWarn(...)
BeginError(...)
BeginFatal(...)
```

Parent-aware overloads accept an active parent entry ID/handle.

## Begin inline entries

```csharp
BeginInline(level, message, ...)
BeginInlineTrace(...)
BeginInlineDebug(...)
BeginInlineInfo(...)
BeginInlineWarn(...)
BeginInlineError(...)
BeginInlineFatal(...)
```

## Entry output

```csharp
Write(message, ...)
Write(entryId, message, ...)

WriteLine(message, ...)
WriteLine(entryId, message, ...)
```

## Completion

```csharp
CompleteEntry()
CompleteEntry(message, ...)
CompleteEntry(entryId, ...)
CompleteEntry(parentEntryId, level, message, ...)

CompleteEntryInline(message, ...)
CompleteEntryInline(entryId, message, ...)
```

## File destination

```csharp
EnableFileLogging(path)
DisableFileLogging()
```

## Physical separator

```csharp
WriteBlankLine()
```

The source contains XML documentation for the public API so IDE IntelliSense should provide parameter and behavioral details directly at call sites.

---

# Project structure

The repository uses a traditional Visual Studio solution layout: the solution and repository-level files live at the root, with one sibling directory per project.

```text
MooreLib.Logger/
├── MooreLib.Logger.sln
├── README.md
├── LICENSE
├── .gitignore
│
├── MooreLib.Logger/
│   ├── MooreLib.Logger.csproj
│   └── ... library source files
│
├── MooreLib.Logger.Tests/
│   ├── MooreLib.Logger.Tests.csproj
│   └── ... automated tests
│
└── MooreLib.Logger.Demo/
    ├── MooreLib.Logger.Demo.csproj
    └── Program.cs
```

The projects have distinct roles:

- **`MooreLib.Logger`** — the library, assembly, and eventual NuGet package;
- **`MooreLib.Logger.Tests`** — automated state-machine, concurrency, filtering, rollover, ownership, and regression tests;
- **`MooreLib.Logger.Demo`** — a small executable playground for visually exercising console behavior, progressive writes, tasks/threads, structured properties, exceptions, and live file output.

The repository/project/package identity is intentionally `MooreLib.Logger`, while public consumer types live under the `MooreLib.Logging` namespace:

```csharp
using MooreLib.Logging;
```

This keeps normal application code natural (`new Logger(...)`) without producing the fully-qualified type name `MooreLib.Logger.Logger`.

Internally, the implementation is split by responsibility while still presenting one public `Logger` facade. Conceptually:

```text
Application
    |
    v
Logger public facade
    |
    v
logical entry state / AsyncLocal context
    |
    v
physical output coordinator
    |
    v
rendered physical event stream
    |             |
    v             v
 console       NLog file target
                   |
                   v
              archive / retention
```

MooreLib owns the semantic state and physical-line rules. NLog remains the backend.

---

# Building

From the repository root:

```bash
dotnet restore
dotnet build -c Release
```

XML documentation output is enabled in the project so the generated documentation accompanies the assembly for IntelliSense/package consumption.

---

# Tests

Run the test suite with:

```bash
dotnet test -c Release
```

The current test project covers the state machine and integration-sensitive behavior, including:

- basic logical entries;
- progressive inline writes;
- interruption and resume;
- multiple competing writers;
- actual Task/thread concurrency;
- `AsyncLocal` flow and restoration;
- strict invalid entry IDs;
- loose parent/child lifetime behavior;
- structured-property inheritance and override;
- reserved-property rejection;
- exception retention/rendering;
- filtering combinations;
- unconditional blank-line output;
- CR/LF/CRLF and trailing-newline preservation;
- file enable/disable during active inline output;
- transactional configuration rollback;
- destination ownership/release;
- disposal behavior;
- size and daily safe-boundary rollover;
- archive retention;
- stable active filename behavior;
- NLog callsite-wrapper behavior.

---

# Design scope

MooreLib.Logger deliberately stays focused.

## In scope

- simple application-facing logging;
- structured properties;
- exceptions;
- logical entries;
- nested/tree presentation;
- progressive partial-line output;
- async ambient entry context;
- optional live file logging;
- basic filtering;
- safe file rollover coordination.

## Intentionally not implemented here

- replacing NLog;
- `ILogger<T>` / category-based DI abstractions;
- arbitrary target-management APIs;
- a new message-template language;
- custom remote logging transports;
- cross-process destination ownership;
- buffering complete logical entries;
- buffering file fragments until newline.

Applications that need additional NLog backend capabilities should generally use NLog itself rather than expanding MooreLib.Logger into a replacement logging framework.

---

# Thread-safety and unusual ordering

MooreLib attempts to preserve valid requested output even when logical entry ordering is unusual.

For example, this is supported:

```csharp
var parent = Log.BeginInfo("Parent");
var child = Log.BeginInfo(parent, "Child");

Log.CompleteEntry(parent);

Log.WriteLine(child, "Child is still active.");
Log.CompleteEntry(child);
```

The resulting tree may be aesthetically unusual, but the already-created child remains valid.

By contrast, this is intentionally rejected:

```csharp
var parent = Log.BeginInfo("Parent");
Log.CompleteEntry(parent);

Log.BeginInfo(parent, "New child"); // throws
```

The guiding distinction is:

```text
valid but unusual ordering      -> best-effort output
invalid explicit entry reference -> throw
idempotent disposal/cleanup      -> harmless no-op
```

---

# Versioning

MooreLib.Logger uses a simple `Major.Revision.0` versioning scheme.

```text
1.13.0  -> revision r13
1.14.0  -> revision r14
1.15.0  -> revision r15
```

The **revision** component is globally monotonic and increments for every released code change, including small fixes. The **major** component changes only for an intentionally breaking public API generation. The patch component is reserved and currently remains `0`.

As a result, a future breaking revision may intentionally move from a version such as `1.56.0` to `2.57.0` rather than resetting the revision component to zero.

This scheme preserves a direct mapping between the historical MooreLib revision number and the published assembly/package version while still providing a clear major-version compatibility signal.

---

# License

MooreLib.Logger is licensed under the **MIT License**.

```text
Copyright (c) 2026 Andrew J. Moore
```

See [`LICENSE`](LICENSE) for the full license text.

Source files use the SPDX identifier:

```text
SPDX-License-Identifier: MIT
```

---

# Status

Version **1.13.0** corresponds to the stabilized **r13** architecture. The feature set is considered substantially complete for its intended purpose: NLog-backed application logging with logical entries and console-style progressive output. Real application usage should drive future revisions rather than speculative feature expansion.

Future changes should favor:

- concrete bug fixes;
- regression tests for observed failures;
- documentation and usability improvements;
- measured performance improvements where profiling identifies a real problem.

The project intentionally avoids expanding into a general replacement for NLog.
