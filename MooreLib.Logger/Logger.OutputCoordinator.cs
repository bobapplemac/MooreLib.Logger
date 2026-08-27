// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace MooreLib.Logging
{
    public sealed partial class Logger
    {
        private bool EmitPhysicalLocked(PhysicalEvent logEvent, LogEntry entry, LogLevel level, bool bypassSeverityFiltering = false)
        {
            ThrowIfNotActiveLocked();

            if (!IsVisibleAtAnyDestinationLocked(level, bypassSeverityFiltering))
            {
                return false;
            }

            AssertEmissionInvariantLocked(logEvent, entry);

            var continuesOpenLine =
                _openPhysicalEntry != null &&
                entry != null &&
                ReferenceEquals(_openPhysicalEntry, entry) &&
                (logEvent.OutputKind == PhysicalOutputKind.Fragment ||
                 logEvent.OutputKind == PhysicalOutputKind.FragmentLineEnd);

            if (_openPhysicalEntry != null && !continuesOpenLine)
            {
                CloseOpenPhysicalLineLocked(markInterrupted: true);
            }

            var startsNewLine =
                logEvent.OutputKind == PhysicalOutputKind.NormalLine ||
                logEvent.OutputKind == PhysicalOutputKind.BlankLine ||
                logEvent.OutputKind == PhysicalOutputKind.HeaderLine ||
                logEvent.OutputKind == PhysicalOutputKind.HeaderLineOpen ||
                logEvent.OutputKind == PhysicalOutputKind.FragmentLine ||
                logEvent.OutputKind == PhysicalOutputKind.FragmentLineOpen ||
                logEvent.OutputKind == PhysicalOutputKind.PrefixedFragmentLine;

            DispatchPhysicalLocked(logEvent, entry, level, startsNewLine, bypassSeverityFiltering);

            if (logEvent.OutputKind == PhysicalOutputKind.HeaderLineOpen || logEvent.OutputKind == PhysicalOutputKind.FragmentLineOpen)
            {
                if (entry == null)
                {
                    throw new InvalidOperationException("An open physical line must belong to a logical entry.");
                }

                if (_openPhysicalEntry != null)
                {
                    throw new InvalidOperationException("A second logical entry cannot claim an already-open physical line.");
                }

                _openPhysicalEntry = entry;
            }
            else if (logEvent.OutputKind == PhysicalOutputKind.FragmentLineEnd)
            {
                if (!ReferenceEquals(_openPhysicalEntry, entry))
                {
                    throw new InvalidOperationException("Cannot end a raw physical fragment line without owning it.");
                }

                _openPhysicalEntry = null;
            }

            return true;
        }

        private void AssertEmissionInvariantLocked(PhysicalEvent logEvent, LogEntry entry)
        {
            if (entry?.State == EntryLifecycleState.Completed)
            {
                throw new InvalidOperationException($"Completed log entry {entry.EntrySequence} cannot emit physical output.");
            }

            if (logEvent.OutputKind == PhysicalOutputKind.Fragment || logEvent.OutputKind == PhysicalOutputKind.FragmentLineEnd)
            {
                if (entry == null || !ReferenceEquals(_openPhysicalEntry, entry))
                {
                    throw new InvalidOperationException("Cannot emit a raw fragment without owning the currently open physical line.");
                }
            }

            if (_openPhysicalEntry != null)
            {
                if (!_activeEntries.Contains(_openPhysicalEntry) || !_openPhysicalEntry.IsActive)
                {
                    throw new InvalidOperationException(
                        "The physical stream references an entry that is no longer active.");
                }

                if (!_openPhysicalEntry.OwnsOpenLine)
                {
                    throw new InvalidOperationException(
                        $"Entry {_openPhysicalEntry.EntrySequence} owns the physical line but is in incompatible state {_openPhysicalEntry.State}.");
                }
            }
        }

        private bool IsConsoleDestinationEnabledLocked() =>
            _usesTestBackend ? _testConsoleLoggingEnabled : _consoleLoggingEnabled;

        private bool IsFileDestinationEnabledLocked() =>
            _usesTestBackend ? _testFileLoggingEnabled : _fileTarget != null;

        private bool IsConsoleVisibleLocked(LogLevel level, bool bypassSeverityFiltering = false) =>
            IsConsoleDestinationEnabledLocked() &&
            (bypassSeverityFiltering || (int)level >= (int)_minimumConsoleLevel);

        private bool IsFileVisibleLocked(LogLevel level, bool bypassSeverityFiltering = false) =>
            IsFileDestinationEnabledLocked() &&
            (bypassSeverityFiltering || (int)level >= (int)_minimumFileLevel);

        private bool IsVisibleAtAnyDestinationLocked(LogLevel level, bool bypassSeverityFiltering = false) =>
            IsConsoleVisibleLocked(level, bypassSeverityFiltering) ||
            IsFileVisibleLocked(level, bypassSeverityFiltering);

        private LogLevel GetPhysicalCommandDispatchLevelLocked()
        {
            var level = LogLevel.Trace;
            if (IsConsoleDestinationEnabledLocked() && (int)_minimumConsoleLevel > (int)level)
            {
                level = _minimumConsoleLevel;
            }
            if (IsFileDestinationEnabledLocked() && (int)_minimumFileLevel > (int)level)
            {
                level = _minimumFileLevel;
            }
            return level;
        }

        private void DispatchPhysicalLocked(
            PhysicalEvent logEvent,
            LogEntry entry,
            LogLevel level,
            bool startsNewPhysicalLine,
            bool bypassSeverityFiltering = false)
        {
            if (_testObserver != null)
            {
                var capturedProperties = logEvent.Properties
                    .ToDictionary(
                        pair => Convert.ToString(pair.Key, CultureInfo.InvariantCulture) ?? string.Empty,
                        pair => pair.Value,
                        StringComparer.Ordinal);

                _testObserver(new PhysicalEmission(
                    level,
                    logEvent.Message ?? string.Empty,
                    logEvent.OutputKind,
                    logEvent.PhysicalPrefix,
                    entry == null ? (Guid?)null : InstanceId,
                    entry?.EntrySequence,
                    logEvent.Exception,
                    capturedProperties,
                    IsConsoleVisibleLocked(level, bypassSeverityFiltering),
                    IsFileVisibleLocked(level, bypassSeverityFiltering)));

                return;
            }

            LogPhysicalLocked(logEvent, level, startsNewPhysicalLine);
        }

        private void CloseOpenPhysicalLineLocked(bool markInterrupted)
        {
            var owner = _openPhysicalEntry;
            if (owner == null)
            {
                return;
            }

            if (!_activeEntries.Contains(owner) || !owner.IsActive)
            {
                throw new InvalidOperationException(
                    $"Cannot close the physical line because owning entry {owner.EntrySequence} is not active.");
            }

            if (!owner.OwnsOpenLine)
            {
                throw new InvalidOperationException(
                    $"Entry {owner.EntrySequence} owns the physical line but is in incompatible state {owner.State}.");
            }

            var lineBreak = CreatePhysicalEvent(
                owner.Level,
                string.Empty,
                null,
                PhysicalOutputKind.ForcedLineBreak);
            ApplyProperties(lineBreak, owner.Properties);
            ApplyReservedEntryProperties(
                lineBreak,
                owner,
                markInterrupted ? EntryEventType.Interrupted : EntryEventType.End);
            DispatchPhysicalLocked(lineBreak, owner, owner.Level, startsNewPhysicalLine: false);
            _openPhysicalEntry = null;

            if (markInterrupted)
            {
                switch (owner.State)
                {
                    case EntryLifecycleState.ActiveLineOpen:
                        owner.State = EntryLifecycleState.ActiveInterrupted;
                        break;

                    case EntryLifecycleState.CompletingLineOpen:
                        owner.State = EntryLifecycleState.CompletingInterrupted;
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Entry {owner.EntrySequence} cannot be interrupted from state {owner.State}.");
                }
            }
            else
            {
                switch (owner.State)
                {
                    case EntryLifecycleState.ActiveLineOpen:
                        owner.State = EntryLifecycleState.ActiveLineClosed;
                        break;

                    case EntryLifecycleState.CompletingLineOpen:
                        owner.State = EntryLifecycleState.CompletingInterrupted;
                        break;

                    default:
                        // Preserve the existing state.
                        break;
                }
            }
        }

        private void CompleteRecordLocked(LogEntry entry)
        {
            if (ReferenceEquals(_openPhysicalEntry, entry))
            {
                CloseOpenPhysicalLineLocked(markInterrupted: false);
            }

            entry.State = EntryLifecycleState.Completed;
            _activeEntries.Remove(entry);

            var current = _currentEntry.Value;
            if (current != null && ReferenceEquals(current.Entry, entry))
            {
                _currentEntry.Value = GetFirstActiveContextLocked(current.Parent);
            }
        }

        private LogEntry ResolveExplicitEntryLocked(LogEntry entry, string role)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            if (!entry.BelongsTo(this))
            {
                throw new ArgumentException(
                    $"The specified {role} belongs to a different Logger instance.",
                    nameof(entry));
            }

            if (!_activeEntries.Contains(entry) || !entry.IsActive)
            {
                throw new InvalidOperationException(
                    $"The specified {role} {entry.EntrySequence} is not active.");
            }

            return entry;
        }

        private LogEntry ResolveAmbientEntryLocked() => GetCurrentContextLocked()?.Entry;

        private EntryContext GetCurrentContextLocked()
        {
            var current = GetFirstActiveContextLocked(_currentEntry.Value);
            if (!ReferenceEquals(current, _currentEntry.Value))
            {
                _currentEntry.Value = current;
            }
            return current;
        }

        private EntryContext GetFirstActiveContextLocked(EntryContext context)
        {
            while (context != null)
            {
                if (_activeEntries.Contains(context.Entry) && context.Entry.IsActive)
                {
                    return context;
                }
                context = context.Parent;
            }
            return null;
        }

        private static EntryContext FindContext(EntryContext context, LogEntry entry)
        {
            while (context != null)
            {
                if (ReferenceEquals(context.Entry, entry)) return context;
                context = context.Parent;
            }
            return null;
        }

        private long NextEntrySequenceLocked() => checked(++_nextEntrySequence);

        private PhysicalEvent CreatePhysicalEvent(
            LogLevel level,
            string message,
            Exception exception,
            PhysicalOutputKind outputKind)
        {
            var result = new PhysicalEvent(ToNLogLevel(level), _options.LoggerName, message, outputKind)
            {
                Exception = exception
            };
            return result;
        }

        private static LogProperty[] MergeProperties(LogProperty[] inherited, LogProperty[] supplied)
        {
            if ((inherited == null || inherited.Length == 0) && (supplied == null || supplied.Length == 0))
            {
                return Array.Empty<LogProperty>();
            }

            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            if (inherited != null)
            {
                foreach (var property in inherited) values[property.Name] = property.Value;
            }
            if (supplied != null)
            {
                foreach (var property in supplied) values[property.Name] = property.Value;
            }

            return values.Select(pair => new LogProperty(pair.Key, pair.Value)).ToArray();
        }

        private static void ValidateProperties(LogProperty[] properties)
        {
            if (properties == null) return;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in properties)
            {
                if (string.IsNullOrWhiteSpace(property.Name))
                {
                    throw new ArgumentException("Property name cannot be null or whitespace.", nameof(properties));
                }

                if (property.Name.StartsWith(ReservedPropertyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Property names beginning with '{ReservedPropertyPrefix}' are reserved for MooreLib.Logger metadata.", nameof(properties));
                }

                if (!seen.Add(property.Name))
                {
                    throw new ArgumentException($"Duplicate log property name '{property.Name}'.", nameof(properties));
                }
            }
        }

        private static void ApplyProperties(NLog.LogEventInfo logEvent, LogProperty[] properties)
        {
            if (properties == null) return;
            foreach (var property in properties) logEvent.Properties[property.Name] = property.Value;
        }

        private void ApplyReservedEntryProperties(NLog.LogEventInfo logEvent, LogEntry entry, EntryEventType entryType)
        {
            logEvent.Properties[InstanceIdPropertyName] = InstanceId;
            logEvent.Properties[EntrySequencePropertyName] = entry.EntrySequence;
            if (entry.Parent != null)
            {
                logEvent.Properties[ParentEntrySequencePropertyName] = entry.Parent.EntrySequence;
            }
            logEvent.Properties[EntryTypePropertyName] = entryType.ToString();
            logEvent.Properties[EntryDepthPropertyName] = entry.Depth;
        }

        private void ThrowIfNotActiveLocked()
        {
            if (_lifecycleState != LoggerLifecycleState.Active)
            {
                throw new ObjectDisposedException(nameof(Logger));
            }
        }
    }
}