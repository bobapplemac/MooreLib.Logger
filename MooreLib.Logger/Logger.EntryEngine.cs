// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

namespace MooreLib.Logging;

public sealed partial class Logger
{
    private void WriteEventCore(
        LogLevel level,
        string message,
        Exception? exception,
        LogProperty[]? properties)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateProperties(properties);

        lock (_coordinatorSync)
        {
            ThrowIfNotActiveLocked();
            _ = ToNLogLevel(level);

            if (!IsVisibleAtAnyDestinationLocked(level))
            {
                return;
            }

            var lines = BuildPhysicalContentLines(level, message, exception);
            for (var i = 0; i < lines.Length; i++)
            {
                var first = i == 0;
                var last = i == lines.Length - 1;
                var outputKind = first
                    ? PhysicalOutputKind.NormalLine
                    : PhysicalOutputKind.PrefixedFragmentLine;

                var logEvent = CreatePhysicalEvent(
                    level,
                    lines[i],
                    first ? exception : null,
                    outputKind);

                if (!first)
                {
                    logEvent.PhysicalPrefix = FormatStandaloneBranchPrefix(last, lines[i]);
                }

                ApplyProperties(logEvent, properties);
                EmitPhysicalLocked(logEvent, entry: null, level);
            }
        }
    }

    private void WriteAttachedEventCore(
        LogEntry parentEntry,
        LogLevel level,
        string message,
        Exception? exception,
        LogProperty[]? properties,
        bool completeParent)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateProperties(properties);

        lock (_coordinatorSync)
        {
            ThrowIfNotActiveLocked();
            _ = ToNLogLevel(level);

            var parent = ResolveExplicitEntryLocked(parentEntry, "parent entry");
            if (parent.IsCompleting)
            {
                throw new InvalidOperationException(
                    $"Cannot attach a new child because parent entry {parent.EntrySequence} is already being completed.");
            }

            var child = new LogEntry(
                this,
                NextEntrySequenceLocked(),
                parent,
                level,
                MergeProperties(parent.Properties, properties));

            if (!IsVisibleAtAnyDestinationLocked(level))
            {
                if (completeParent)
                {
                    CompleteRecordLocked(parent);
                }
                return;
            }

            var lines = BuildPhysicalContentLines(level, message, exception);
            for (var i = 0; i < lines.Length; i++)
            {
                var first = i == 0;
                var last = i == lines.Length - 1;
                var logEvent = CreatePhysicalEvent(
                    level,
                    lines[i],
                    first ? exception : null,
                    first ? PhysicalOutputKind.HeaderLine : PhysicalOutputKind.PrefixedFragmentLine);

                if (first)
                {
                    logEvent.PhysicalPrefix = FormatEntryBeginPrefix(child, terminal: completeParent, lines[i]);
                }
                else if (completeParent)
                {
                    logEvent.Message = FormatTerminalChildContentLine(child, terminal: last, lines[i]);
                }
                else
                {
                    logEvent.Message = FormatEntryContentLine(child, terminal: last, lines[i]);
                }

                ApplyProperties(logEvent, child.Properties);
                ApplyReservedEntryProperties(
                    logEvent,
                    child,
                    completeParent && last ? EntryEventType.AttachedEnd : EntryEventType.Attached);
                if (EmitPhysicalLocked(logEvent, child, level))
                {
                    parent.HasVisibleTreeContent = true;
                    if (!first)
                    {
                        child.HasVisibleTreeContent = true;
                    }
                }
            }

            if (completeParent)
            {
                CompleteRecordLocked(parent);
            }
        }
    }

    private LogEntry BeginEntryCore(
        LogLevel level,
        string message,
        bool leaveLineOpen,
        LogEntry? explicitParent,
        bool useExplicitParent,
        Exception? exception,
        LogProperty[]? properties)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateProperties(properties);

        lock (_coordinatorSync)
        {
            ThrowIfNotActiveLocked();
            _ = ToNLogLevel(level);

            var inheritedContext = GetCurrentContextLocked();
            LogEntry? parent = null;
            EntryContext? parentContext = null;

            if (useExplicitParent)
            {
                parent = ResolveExplicitEntryLocked(explicitParent!, "parent entry");
                if (parent.IsCompleting)
                {
                    throw new InvalidOperationException(
                        $"Cannot begin a child because parent entry {parent.EntrySequence} is already being completed.");
                }

                parentContext = FindContext(inheritedContext, parent)
                    ?? new EntryContext(parent, inheritedContext);
            }
            else if (inheritedContext is not null)
            {
                parent = inheritedContext.Entry;
                if (parent.IsCompleting)
                {
                    throw new InvalidOperationException(
                        $"Cannot begin a nested entry because current entry {parent.EntrySequence} is already being completed.");
                }
                parentContext = inheritedContext;
            }

            var entry = new LogEntry(
                this,
                NextEntrySequenceLocked(),
                parent,
                level,
                MergeProperties(parent?.Properties, properties));

            _activeEntries.Add(entry);
            _currentEntry.Value = new EntryContext(entry, parentContext);

            var visible = IsVisibleAtAnyDestinationLocked(level);

            if (leaveLineOpen)
            {
                if (visible)
                {
                    var text = NormalizeInlineMessage(message);
                    var first = CreatePhysicalEvent(
                        level,
                        text,
                        exception,
                        PhysicalOutputKind.HeaderLineOpen);

                    if (entry.Parent is not null)
                    {
                        first.PhysicalPrefix = FormatEntryBeginPrefix(entry, terminal: false, text);
                    }
                    else
                    {
                        first.Message = FormatRootBegin(text);
                    }

                    ApplyProperties(first, entry.Properties);
                    ApplyReservedEntryProperties(first, entry, EntryEventType.BeginInline);
                    entry.State = EntryLifecycleState.ActiveLineOpen;
                    try
                    {
                        if (!EmitPhysicalLocked(first, entry, level))
                        {
                            entry.State = EntryLifecycleState.ActiveLineClosed;
                        }
                        else if (entry.Parent is not null)
                        {
                            entry.Parent.HasVisibleTreeContent = true;
                        }
                    }
                    catch
                    {
                        entry.State = EntryLifecycleState.ActiveLineClosed;
                        throw;
                    }
                }
                else
                {
                    entry.State = EntryLifecycleState.ActiveLineClosed;
                }
            }
            else
            {
                if (visible)
                {
                    var lines = BuildPhysicalContentLines(level, message, exception);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        var firstLine = i == 0;
                        var logEvent = CreatePhysicalEvent(
                            level,
                            firstLine && entry.Parent is null
                                ? FormatRootBegin(lines[i])
                                : lines[i],
                            firstLine ? exception : null,
                            firstLine
                                ? (entry.Parent is not null ? PhysicalOutputKind.HeaderLine : PhysicalOutputKind.NormalLine)
                                : PhysicalOutputKind.FragmentLine);

                        if (firstLine && entry.Parent is not null)
                        {
                            logEvent.PhysicalPrefix = FormatEntryBeginPrefix(entry, terminal: false, lines[i]);
                        }
                        else if (!firstLine)
                        {
                            logEvent.Message = FormatContinuationLine(entry, lines[i]);
                        }

                        ApplyProperties(logEvent, entry.Properties);
                        ApplyReservedEntryProperties(
                            logEvent,
                            entry,
                            firstLine ? EntryEventType.Begin : EntryEventType.Continuation);
                        if (EmitPhysicalLocked(logEvent, entry, level))
                        {
                            if (firstLine && entry.Parent is not null)
                            {
                                entry.Parent.HasVisibleTreeContent = true;
                            }
                            if (!firstLine)
                            {
                                entry.HasVisibleTreeContent = true;
                            }
                        }
                    }
                }

                entry.State = EntryLifecycleState.ActiveLineClosed;
            }

            return entry;
        }
    }

    private void WriteCore(
        LogEntry? explicitEntry,
        bool useExplicitEntry,
        string message,
        bool endLine,
        LogProperty[]? properties)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateProperties(properties);

        lock (_coordinatorSync)
        {
            ThrowIfNotActiveLocked();
            var entry = useExplicitEntry
                ? ResolveExplicitEntryLocked(explicitEntry!, "entry")
                : ResolveAmbientEntryLocked();

            if (entry is null)
            {
                WriteStandaloneInfoLocked(message, properties);
                return;
            }

            if (entry.State == EntryLifecycleState.Completed)
            {
                throw new InvalidOperationException($"Log entry {entry.EntrySequence} is no longer active.");
            }

            if (entry.IsCompleting && endLine)
            {
                throw new InvalidOperationException(
                    "The entry is already on its terminal inline line. Use Write(...) followed by CompleteEntry().");
            }

            if (!endLine)
            {
                WriteInlineFragmentLocked(entry, NormalizeInlineMessage(message), properties);
                return;
            }

            WriteClosedPhysicalLinesLocked(entry, SplitPhysicalLines(message), properties);
        }
    }

    private void WriteInlineFragmentLocked(LogEntry entry, string text, LogProperty[]? properties)
    {
        if (!IsVisibleAtAnyDestinationLocked(entry.Level))
        {
            return;
        }

        PhysicalEvent logEvent;
        EntryEventType eventType;
        EntryLifecycleState emittedState;

        switch (entry.State)
        {
            case EntryLifecycleState.ActiveLineOpen:
                logEvent = CreatePhysicalEvent(entry.Level, text, exception: null, PhysicalOutputKind.Fragment);
                eventType = EntryEventType.Continuation;
                emittedState = EntryLifecycleState.ActiveLineOpen;
                break;

            case EntryLifecycleState.CompletingLineOpen:
                logEvent = CreatePhysicalEvent(entry.Level, text, exception: null, PhysicalOutputKind.Fragment);
                eventType = EntryEventType.Continuation;
                emittedState = EntryLifecycleState.CompletingLineOpen;
                break;

            case EntryLifecycleState.ActiveInterrupted:
                logEvent = CreatePhysicalEvent(
                    entry.Level,
                    FormatInlineResume(entry, text),
                    exception: null,
                    PhysicalOutputKind.HeaderLineOpen);
                eventType = EntryEventType.Resume;
                emittedState = EntryLifecycleState.ActiveLineOpen;
                break;

            case EntryLifecycleState.CompletingInterrupted:
                logEvent = CreatePhysicalEvent(
                    entry.Level,
                    FormatInlineResume(entry, text),
                    exception: null,
                    PhysicalOutputKind.HeaderLineOpen);
                eventType = EntryEventType.Resume;
                emittedState = EntryLifecycleState.CompletingLineOpen;
                break;

            case EntryLifecycleState.CompletingLinePending:
                logEvent = CreatePhysicalEvent(
                    entry.Level,
                    FormatEndLine(entry, text),
                    exception: null,
                    PhysicalOutputKind.FragmentLineOpen);
                eventType = EntryEventType.EndInline;
                emittedState = EntryLifecycleState.CompletingLineOpen;
                break;

            case EntryLifecycleState.ActiveLineClosed:
                logEvent = CreatePhysicalEvent(
                    entry.Level,
                    FormatContinuationLine(entry, text),
                    exception: null,
                    PhysicalOutputKind.FragmentLineOpen);
                eventType = EntryEventType.Continuation;
                emittedState = EntryLifecycleState.ActiveLineOpen;
                break;

            default:
                throw new InvalidOperationException($"Log entry {entry.EntrySequence} cannot accept inline output in state {entry.State}.");
        }

        ApplyProperties(logEvent, entry.Properties);
        ApplyProperties(logEvent, properties);
        ApplyReservedEntryProperties(logEvent, entry, eventType);

        var previousState = entry.State;
        if (emittedState is EntryLifecycleState.ActiveLineOpen or EntryLifecycleState.CompletingLineOpen)
        {
            entry.State = emittedState;
        }

        try
        {
            if (!EmitPhysicalLocked(logEvent, entry, entry.Level))
            {
                entry.State = previousState;
            }
            else if (previousState == EntryLifecycleState.ActiveLineClosed)
            {
                entry.HasVisibleTreeContent = true;
            }
        }
        catch
        {
            entry.State = previousState;
            throw;
        }
    }

    private void WriteClosedPhysicalLinesLocked(
        LogEntry entry,
        string[] lines,
        LogProperty[]? properties)
    {
        if (!IsVisibleAtAnyDestinationLocked(entry.Level))
        {
            return;
        }

        var lineIndex = 0;

        if (entry.State == EntryLifecycleState.ActiveLineOpen)
        {
            var end = CreatePhysicalEvent(
                entry.Level,
                lines[0],
                exception: null,
                PhysicalOutputKind.FragmentLineEnd);
            ApplyProperties(end, entry.Properties);
            ApplyProperties(end, properties);
            ApplyReservedEntryProperties(end, entry, EntryEventType.Continuation);
            EmitPhysicalLocked(end, entry, entry.Level);
            entry.State = EntryLifecycleState.ActiveLineClosed;
            lineIndex = 1;
        }
        else if (entry.State == EntryLifecycleState.ActiveInterrupted)
        {
            var resume = CreatePhysicalEvent(
                entry.Level,
                FormatInlineResume(entry, lines[0]),
                exception: null,
                PhysicalOutputKind.HeaderLine);
            ApplyProperties(resume, entry.Properties);
            ApplyProperties(resume, properties);
            ApplyReservedEntryProperties(resume, entry, EntryEventType.Resume);
            EmitPhysicalLocked(resume, entry, entry.Level);
            entry.State = EntryLifecycleState.ActiveLineClosed;
            lineIndex = 1;
        }
        else if (entry.IsCompleting)
        {
            throw new InvalidOperationException(
                "WriteLine cannot be used after CompleteEntryInline(). Use Write(...) followed by CompleteEntry().");
        }

        for (; lineIndex < lines.Length; lineIndex++)
        {
            var line = CreatePhysicalEvent(
                entry.Level,
                FormatContinuationLine(entry, lines[lineIndex]),
                exception: null,
                PhysicalOutputKind.FragmentLine);
            ApplyProperties(line, entry.Properties);
            ApplyProperties(line, properties);
            ApplyReservedEntryProperties(line, entry, EntryEventType.Continuation);
            if (EmitPhysicalLocked(line, entry, entry.Level))
            {
                entry.HasVisibleTreeContent = true;
            }
        }

        entry.State = EntryLifecycleState.ActiveLineClosed;
    }

    private void CompleteEntryInlineCore(
        LogEntry? explicitEntry,
        bool useExplicitEntry,
        string message,
        LogProperty[]? properties)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateProperties(properties);

        lock (_coordinatorSync)
        {
            ThrowIfNotActiveLocked();
            var entry = useExplicitEntry
                ? ResolveExplicitEntryLocked(explicitEntry!, "entry")
                : ResolveAmbientEntryLocked()
                    ?? throw new InvalidOperationException("No active logical entry is available to complete inline.");

            if (entry.IsCompleting)
            {
                throw new InvalidOperationException("The entry is already being completed inline.");
            }

            if (entry.State == EntryLifecycleState.ActiveLineOpen)
            {
                throw new InvalidOperationException(
                    "CompleteEntryInline can only start a new terminal line. End the current physical line with WriteLine() first.");
            }

            var normalized = NormalizeInlineMessage(message);
            if (!IsVisibleAtAnyDestinationLocked(entry.Level))
            {
                entry.State = EntryLifecycleState.CompletingLinePending;
                return;
            }

            var terminal = CreatePhysicalEvent(
                entry.Level,
                FormatEndLine(entry, normalized),
                exception: null,
                PhysicalOutputKind.FragmentLineOpen);
            ApplyProperties(terminal, entry.Properties);
            ApplyProperties(terminal, properties);
            ApplyReservedEntryProperties(terminal, entry, EntryEventType.EndInline);

            var previousState = entry.State;
            entry.State = EntryLifecycleState.CompletingLineOpen;
            try
            {
                if (!EmitPhysicalLocked(terminal, entry, entry.Level))
                {
                    entry.State = EntryLifecycleState.CompletingLinePending;
                }
            }
            catch
            {
                entry.State = previousState;
                throw;
            }
        }
    }

    private void CompleteEntryCore(
        LogEntry? explicitEntry,
        bool useExplicitEntry,
        string? message,
        Exception? exception,
        LogProperty[]? properties)
    {
        ValidateProperties(properties);

        lock (_coordinatorSync)
        {
            ThrowIfNotActiveLocked();
            var entry = useExplicitEntry
                ? ResolveExplicitEntryLocked(explicitEntry!, "entry")
                : ResolveAmbientEntryLocked();

            if (entry is null)
            {
                return;
            }

            CompleteEntryLocked(entry, message, exception, properties);
        }
    }

    private void CompleteEntryLocked(
        LogEntry entry,
        string? message,
        Exception? exception,
        LogProperty[]? properties)
    {
        if (entry.State == EntryLifecycleState.Completed)
        {
            throw new InvalidOperationException($"Log entry {entry.EntrySequence} is no longer active.");
        }

        var lines = BuildOptionalCompletionLines(entry.Level, message, exception);

        if (entry.IsCompleting && exception is not null)
        {
            throw new InvalidOperationException(
                "An exception cannot be appended after CompleteEntryInline() has already committed the terminal line.");
        }

        if (!IsVisibleAtAnyDestinationLocked(entry.Level))
        {
            CompleteRecordLocked(entry);
            return;
        }

        var blankCompletion = exception is null && string.IsNullOrEmpty(message);
        if (blankCompletion &&
            entry.State is EntryLifecycleState.ActiveLineClosed or EntryLifecycleState.ActiveInterrupted)
        {
            if (entry.HasVisibleTreeContent)
            {
                var closure = CreatePhysicalEvent(
                    entry.Level,
                    FormatTreeClosureLine(entry),
                    null,
                    PhysicalOutputKind.FragmentLine);
                ApplyProperties(closure, entry.Properties);
                ApplyProperties(closure, properties);
                ApplyReservedEntryProperties(closure, entry, EntryEventType.End);
                EmitPhysicalLocked(closure, entry, entry.Level);
            }

            CompleteRecordLocked(entry);
            return;
        }

        var exceptionAttached = false;

        if (entry.State == EntryLifecycleState.CompletingLinePending)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var last = i == lines.Length - 1;
                var physical = CreatePhysicalEvent(
                    entry.Level,
                    last ? FormatEndLine(entry, lines[i]) : FormatContinuationLine(entry, lines[i]),
                    null,
                    PhysicalOutputKind.FragmentLine);
                ApplyProperties(physical, entry.Properties);
                ApplyProperties(physical, properties);
                ApplyReservedEntryProperties(
                    physical,
                    entry,
                    last ? EntryEventType.End : EntryEventType.Continuation);
                EmitPhysicalLocked(physical, entry, entry.Level);
            }

            CompleteRecordLocked(entry);
            return;
        }

        if (entry.State == EntryLifecycleState.CompletingLineOpen)
        {
            var text = lines.Length > 0 ? lines[0] : string.Empty;
            var end = CreatePhysicalEvent(entry.Level, text, null, PhysicalOutputKind.FragmentLineEnd);
            ApplyProperties(end, entry.Properties);
            ApplyProperties(end, properties);
            ApplyReservedEntryProperties(end, entry, EntryEventType.End);
            EmitPhysicalLocked(end, entry, entry.Level);

            // Preserve best-effort behavior for unusual multiline completion text after
            // CompleteEntryInline(). The terminal marker has already been emitted, so any
            // additional physical lines resume rather than pretending to be ordinary children.
            for (var i = 1; i < lines.Length; i++)
            {
                var last = i == lines.Length - 1;
                var continuation = CreatePhysicalEvent(
                    entry.Level,
                    FormatInlineResume(entry, lines[i]),
                    null,
                    PhysicalOutputKind.HeaderLine);
                ApplyProperties(continuation, entry.Properties);
                ApplyProperties(continuation, properties);
                ApplyReservedEntryProperties(
                    continuation,
                    entry,
                    last ? EntryEventType.ResumeEnd : EntryEventType.Resume);
                EmitPhysicalLocked(continuation, entry, entry.Level);
            }

            CompleteRecordLocked(entry);
            return;
        }

        if (entry.State == EntryLifecycleState.CompletingInterrupted)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var last = i == lines.Length - 1;
                var continuation = CreatePhysicalEvent(
                    entry.Level,
                    FormatInlineResume(entry, lines[i]),
                    null,
                    PhysicalOutputKind.HeaderLine);
                ApplyProperties(continuation, entry.Properties);
                ApplyProperties(continuation, properties);
                ApplyReservedEntryProperties(
                    continuation,
                    entry,
                    last ? EntryEventType.ResumeEnd : EntryEventType.Resume);
                EmitPhysicalLocked(continuation, entry, entry.Level);
            }

            CompleteRecordLocked(entry);
            return;
        }

        if (entry.State == EntryLifecycleState.ActiveLineOpen)
        {
            var firstText = lines.Length > 0 ? lines[0] : string.Empty;
            var endOpen = CreatePhysicalEvent(
                entry.Level,
                firstText,
                exception,
                PhysicalOutputKind.FragmentLineEnd);
            exceptionAttached = exception is not null;
            ApplyProperties(endOpen, entry.Properties);
            ApplyProperties(endOpen, properties);
            ApplyReservedEntryProperties(
                endOpen,
                entry,
                lines.Length <= 1 ? EntryEventType.End : EntryEventType.Continuation);
            EmitPhysicalLocked(endOpen, entry, entry.Level);
            entry.State = EntryLifecycleState.ActiveLineClosed;

            for (var i = 1; i < lines.Length; i++)
            {
                var last = i == lines.Length - 1;
                var continuation = CreatePhysicalEvent(
                    entry.Level,
                    last ? FormatEndLine(entry, lines[i]) : FormatContinuationLine(entry, lines[i]),
                    exceptionAttached ? null : exception,
                    PhysicalOutputKind.FragmentLine);
                exceptionAttached = exceptionAttached || exception is not null;
                ApplyProperties(continuation, entry.Properties);
                ApplyProperties(continuation, properties);
                ApplyReservedEntryProperties(
                    continuation,
                    entry,
                    last ? EntryEventType.End : EntryEventType.Continuation);
                EmitPhysicalLocked(continuation, entry, entry.Level);
            }
        }
        else if (entry.State == EntryLifecycleState.ActiveInterrupted)
        {
            if (lines.Length > 0)
            {
                var resume = CreatePhysicalEvent(
                    entry.Level,
                    FormatInlineResume(entry, lines[0]),
                    exception,
                    PhysicalOutputKind.HeaderLine);
                exceptionAttached = exception is not null;
                ApplyProperties(resume, entry.Properties);
                ApplyProperties(resume, properties);
                ApplyReservedEntryProperties(
                    resume,
                    entry,
                    lines.Length == 1 ? EntryEventType.ResumeEnd : EntryEventType.Resume);
                EmitPhysicalLocked(resume, entry, entry.Level);

                for (var i = 1; i < lines.Length; i++)
                {
                    var last = i == lines.Length - 1;
                    var continuation = CreatePhysicalEvent(
                        entry.Level,
                        last ? FormatEndLine(entry, lines[i]) : FormatContinuationLine(entry, lines[i]),
                        exceptionAttached ? null : exception,
                        PhysicalOutputKind.FragmentLine);
                    exceptionAttached = exceptionAttached || exception is not null;
                    ApplyProperties(continuation, entry.Properties);
                    ApplyProperties(continuation, properties);
                    ApplyReservedEntryProperties(
                        continuation,
                        entry,
                        last ? EntryEventType.End : EntryEventType.Continuation);
                    EmitPhysicalLocked(continuation, entry, entry.Level);
                }
            }
        }
        else
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var last = i == lines.Length - 1;
                var physical = CreatePhysicalEvent(
                    entry.Level,
                    last ? FormatEndLine(entry, lines[i]) : FormatContinuationLine(entry, lines[i]),
                    exceptionAttached ? null : exception,
                    PhysicalOutputKind.FragmentLine);
                exceptionAttached = exceptionAttached || exception is not null;
                ApplyProperties(physical, entry.Properties);
                ApplyProperties(physical, properties);
                ApplyReservedEntryProperties(
                    physical,
                    entry,
                    last ? EntryEventType.End : EntryEventType.Continuation);
                EmitPhysicalLocked(physical, entry, entry.Level);
            }
        }

        CompleteRecordLocked(entry);
    }

    private string[] BuildPhysicalContentLines(LogLevel level, string message, Exception? exception)
    {
        var messageLines = SplitPhysicalLines(message);
        if (exception is null)
        {
            return messageLines;
        }

        return messageLines.Concat(RenderExceptionLines(level, exception)).ToArray();
    }

    private string[] BuildOptionalCompletionLines(LogLevel level, string? message, Exception? exception)
    {
        var lines = new List<string>();
        if (message is not null)
        {
            lines.AddRange(SplitPhysicalLines(message));
        }
        if (exception is not null)
        {
            lines.AddRange(RenderExceptionLines(level, exception));
        }
        return lines.ToArray();
    }

    private void WriteStandaloneInfoLocked(string message, LogProperty[]? properties)
    {
        var lines = SplitPhysicalLines(message);
        for (var i = 0; i < lines.Length; i++)
        {
            var first = i == 0;
            var last = i == lines.Length - 1;
            var logEvent = CreatePhysicalEvent(
                LogLevel.Info,
                lines[i],
                null,
                first ? PhysicalOutputKind.NormalLine : PhysicalOutputKind.PrefixedFragmentLine);
            if (!first) logEvent.PhysicalPrefix = FormatStandaloneBranchPrefix(last, lines[i]);
            ApplyProperties(logEvent, properties);
            EmitPhysicalLocked(logEvent, null, LogLevel.Info);
        }
    }
}
