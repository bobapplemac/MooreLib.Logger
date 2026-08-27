// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;

using NLogEventInfo = NLog.LogEventInfo;

namespace MooreLib.Logging
{
    public sealed partial class Logger
    {
        internal const string ReservedPropertyPrefix = "MooreLib.Logger.";
        internal const string InstanceIdPropertyName = ReservedPropertyPrefix + "InstanceId";
        internal const string EntrySequencePropertyName = ReservedPropertyPrefix + "EntrySequence";
        internal const string ParentEntrySequencePropertyName = ReservedPropertyPrefix + "ParentEntrySequence";
        internal const string EntryTypePropertyName = ReservedPropertyPrefix + "EntryType";
        internal const string EntryDepthPropertyName = ReservedPropertyPrefix + "EntryDepth";

        private readonly object _coordinatorSync = new object();

        internal readonly struct PhysicalEmission : IEquatable<PhysicalEmission>
        {
            public LogLevel Level { get; }

            public string Message { get; }

            public PhysicalOutputKind Kind { get; }

            public string Prefix { get; }

            public Guid? InstanceId { get; }

            public long? EntrySequence { get; }

            public Exception Exception { get; }

            public IReadOnlyDictionary<string, object> Properties { get; }

            public bool ConsoleVisible { get; }

            public bool FileVisible { get; }

            public PhysicalEmission(
                LogLevel level,
                string message,
                PhysicalOutputKind kind,
                string prefix,
                Guid? instanceId,
                long? entrySequence,
                Exception exception,
                IReadOnlyDictionary<string, object> properties,
                bool consoleVisible,
                bool fileVisible)
            {
                Level = level;
                Message = message;
                Kind = kind;
                Prefix = prefix;
                InstanceId = instanceId;
                EntrySequence = entrySequence;
                Exception = exception;
                Properties = properties;
                ConsoleVisible = consoleVisible;
                FileVisible = fileVisible;
            }

            public bool Equals(PhysicalEmission other)
            {
                return Level == other.Level &&
                       string.Equals(Message, other.Message, StringComparison.Ordinal) &&
                       Kind == other.Kind &&
                       string.Equals(Prefix, other.Prefix, StringComparison.Ordinal) &&
                       InstanceId == other.InstanceId &&
                       EntrySequence == other.EntrySequence &&
                       Equals(Exception, other.Exception) &&
                       Equals(Properties, other.Properties) &&
                       ConsoleVisible == other.ConsoleVisible &&
                       FileVisible == other.FileVisible;
            }

            public override bool Equals(object obj)
            {
                return obj is PhysicalEmission && Equals((PhysicalEmission)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = 17;
                    hashCode = (hashCode * 31) + Level.GetHashCode();
                    hashCode = (hashCode * 31) + (Message != null ? Message.GetHashCode() : 0);
                    hashCode = (hashCode * 31) + Kind.GetHashCode();
                    hashCode = (hashCode * 31) + (Prefix != null ? Prefix.GetHashCode() : 0);
                    hashCode = (hashCode * 31) + InstanceId.GetHashCode();
                    hashCode = (hashCode * 31) + EntrySequence.GetHashCode();
                    hashCode = (hashCode * 31) + (Exception != null ? Exception.GetHashCode() : 0);
                    hashCode = (hashCode * 31) + (Properties != null ? Properties.GetHashCode() : 0);
                    hashCode = (hashCode * 31) + ConsoleVisible.GetHashCode();
                    hashCode = (hashCode * 31) + FileVisible.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(PhysicalEmission left, PhysicalEmission right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(PhysicalEmission left, PhysicalEmission right)
            {
                return !left.Equals(right);
            }
        }

        private readonly Action<PhysicalEmission> _testObserver;
        private readonly bool _usesTestBackend;
        private bool _consoleLoggingEnabled;
        private bool _testConsoleLoggingEnabled;
        private bool _testFileLoggingEnabled;
        private LogLevel _minimumConsoleLevel;
        private LogLevel _minimumFileLevel;
        private Action _testFlushHook;
        private Action<string> _testConfigurationApplyHook;
        private readonly HashSet<LogEntry> _activeEntries = new HashSet<LogEntry>(ReferenceComparer<LogEntry>.Instance);
        private readonly AsyncLocal<EntryContext> _currentEntry = new AsyncLocal<EntryContext>();

        private long _nextEntrySequence;
        private LogEntry _openPhysicalEntry;
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
            public EntryContext(LogEntry entry, EntryContext parent)
            {
                Entry = entry;
                Parent = parent;
            }

            public LogEntry Entry { get; }
            public EntryContext Parent { get; }
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

        private sealed class ReferenceComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public static readonly ReferenceComparer<T> Instance =
                new ReferenceComparer<T>();

            private ReferenceComparer()
            {
            }

            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}