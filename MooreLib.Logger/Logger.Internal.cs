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
    internal const string EntryIdPropertyName = ReservedPropertyPrefix + "EntryId";
    internal const string ParentEntryIdPropertyName = ReservedPropertyPrefix + "ParentEntryId";
    internal const string EntryTypePropertyName = ReservedPropertyPrefix + "EntryType";
    internal const string EntryDepthPropertyName = ReservedPropertyPrefix + "EntryDepth";

    private readonly object _coordinatorSync = new();

    internal readonly record struct PhysicalEmission(
        LogLevel Level,
        string Message,
        PhysicalOutputKind Kind,
        string Prefix,
        long? EntryId,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties,
        bool ConsoleVisible,
        bool FileVisible);

    private readonly Action<PhysicalEmission>? _testObserver;
    private readonly bool _usesTestBackend;
    private bool _testConsoleLoggingEnabled;
    private bool _testFileLoggingEnabled;
    private LogLevel _minimumConsoleLevel;
    private LogLevel _minimumFileLevel;
    private Action? _testFlushHook;
    private Action<string?>? _testConfigurationApplyHook;
    private readonly Dictionary<long, EntryRecord> _activeEntries = new();
    private readonly AsyncLocal<EntryContext?> _currentEntry = new();

    private long _nextEntryId;
    private long? _openPhysicalEntryId;
    private LogLevel _openPhysicalEntryLevel;
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

    internal sealed class EntryRecord
    {
        public EntryRecord(
            long id,
            EntryRecord? parent,
            LogLevel level,
            LogProperty[] properties)
        {
            Id = id;
            Parent = parent;
            ParentEntryId = parent?.Id;
            Level = level;
            Properties = properties;
            Depth = parent is null ? 0 : checked(parent.Depth + 1);
            State = EntryLifecycleState.ActiveLineClosed;
        }

        public long Id { get; }
        public EntryRecord? Parent { get; }
        public long? ParentEntryId { get; }
        public LogLevel Level { get; }
        public LogProperty[] Properties { get; }
        public int Depth { get; }
        public EntryLifecycleState State { get; set; }
        public bool HasVisibleTreeContent { get; set; }

        public bool IsActive => State != EntryLifecycleState.Completed;
        public bool OwnsOpenLine => State is EntryLifecycleState.ActiveLineOpen or EntryLifecycleState.CompletingLineOpen;
        public bool NeedsResume => State is EntryLifecycleState.ActiveInterrupted or EntryLifecycleState.CompletingInterrupted;
        public bool IsCompleting => State is EntryLifecycleState.CompletingLinePending or EntryLifecycleState.CompletingLineOpen or EntryLifecycleState.CompletingInterrupted;
    }

    internal sealed class EntryContext
    {
        public EntryContext(EntryRecord entry, EntryContext? parent)
        {
            Entry = entry;
            Parent = parent;
        }

        public EntryRecord Entry { get; }
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
