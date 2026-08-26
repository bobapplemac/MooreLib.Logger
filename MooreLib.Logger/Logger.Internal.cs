// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;
using System.Collections.Generic;
using System.Threading;
using NLogEventInfo = NLog.LogEventInfo;

namespace MooreLib.Logging;

public sealed partial class Logger
{
    internal const string ReservedPropertyPrefix = "MooreLib.Logger.";
    internal const string InstanceIdPropertyName = ReservedPropertyPrefix + "InstanceId";
    internal const string EntrySequencePropertyName = ReservedPropertyPrefix + "EntrySequence";
    internal const string ParentEntrySequencePropertyName = ReservedPropertyPrefix + "ParentEntrySequence";
    internal const string EntryTypePropertyName = ReservedPropertyPrefix + "EntryType";
    internal const string EntryDepthPropertyName = ReservedPropertyPrefix + "EntryDepth";

    private readonly object _coordinatorSync = new();

    internal readonly record struct PhysicalEmission(
        LogLevel Level,
        string Message,
        PhysicalOutputKind Kind,
        string Prefix,
        Guid? InstanceId,
        long? EntrySequence,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties,
        bool ConsoleVisible,
        bool FileVisible);

    private readonly Action<PhysicalEmission>? _testObserver;
    private readonly bool _usesTestBackend;
    private bool _consoleLoggingEnabled;
    private bool _testConsoleLoggingEnabled;
    private bool _testFileLoggingEnabled;
    private LogLevel _minimumConsoleLevel;
    private LogLevel _minimumFileLevel;
    private Action? _testFlushHook;
    private Action<string?>? _testConfigurationApplyHook;
    private readonly HashSet<LogEntry> _activeEntries = new(ReferenceEqualityComparer.Instance);
    private readonly AsyncLocal<EntryContext?> _currentEntry = new();

    private long _nextEntrySequence;
    private LogEntry? _openPhysicalEntry;
    private LoggerLifecycleState _lifecycleState = LoggerLifecycleState.Active;

    internal enum EntryLifecycleState
    {
        ActiveLineClosed,
        ActiveLineOpen,
        ActiveInterrupted,
        CompletingLinePending,
        CompletingLineOpen,
        CompletingInterrupted,
        Completed
    }

    private enum LoggerLifecycleState
    {
        Active,
        Disposing,
        Disposed
    }

    internal enum EntryEventType
    {
        Begin,
        BeginInline,
        Continuation,
        Resume,
        ResumeEnd,
        End,
        EndInline,
        Attached,
        AttachedEnd,
        Interrupted
    }

    internal enum PhysicalOutputKind
    {
        NormalLine,
        BlankLine,
        HeaderLine,
        HeaderLineOpen,
        Fragment,
        FragmentLine,
        FragmentLineOpen,
        FragmentLineEnd,
        PrefixedFragmentLine,
        ForcedLineBreak
    }

    internal sealed class EntryContext
    {
        public EntryContext(LogEntry entry, EntryContext? parent)
        {
            Entry = entry;
            Parent = parent;
        }

        public LogEntry Entry { get; }
        public EntryContext? Parent { get; }
    }

    internal sealed class PhysicalEvent : NLogEventInfo
    {
        public PhysicalEvent(NLog.LogLevel level, string loggerName, string message, PhysicalOutputKind outputKind)
            : base(level, loggerName, message)
        {
            OutputKind = outputKind;
        }

        public PhysicalOutputKind OutputKind { get; set; }
        public string PhysicalPrefix { get; set; } = string.Empty;
    }
}
