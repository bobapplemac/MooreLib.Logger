// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore
//
// ------------------------------------------------------------------------------------------
// File:        Logger.cs
// Revision:    r21
// Modified:    2026-08-26
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/MooreLib.Logger
// Description: Public facade for MooreLib.Logger, a structured/context-aware logging wrapper
//              around an instance-owned NLog LogFactory. MooreLib supplies logical entries,
//              incremental physical-line output, parent/child tree rendering, AsyncLocal
//              ambient context, structured properties, and deterministic entry handles while
//              NLog remains responsible for physical targets and file/archive mechanics.
//
//              Revision r19 adds independently configurable console destination state plus configurable
//              stdout/stderr severity routing while preserving the existing coordinated physical-stream
//              semantics and transactional NLog reconfiguration model.
// ------------------------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Runtime.ExceptionServices;
using NLogFactory = NLog.LogFactory;

namespace MooreLib.Logging
{
    /// <summary>
    /// Provides application-facing structured logging with conventional severity methods plus logical multi-line,
    /// inline, nested, and explicitly addressable entries.
    /// </summary>
    public sealed partial class Logger : IDisposable
    {
        /// <summary>Default maximum active log-file size for size-based archival.</summary>
        public const long DefaultMaximumFileSizeBytes = 10L * 1024L * 1024L;

        /// <summary>Default number of archives retained by size-based archival.</summary>
        public const int DefaultMaximumArchiveFiles = 5;

        /// <summary>Sentinel value indicating that archive-count retention is disabled.</summary>
        public const int UnlimitedArchiveFiles = -1;

        /// <summary>Gets the globally unique identifier assigned to this logger instance.</summary>
        public Guid InstanceId => _instanceId;

        private readonly LoggerOptions _options;

        /// <summary>Initializes a logger using the configured initial console state; file logging remains disabled until enabled explicitly.</summary>
        /// <param name="options">Logger configuration, or <see langword="null"/> to use defaults.</param>
        public Logger(LoggerOptions options = null)
        {
            _testObserver = null;
            _usesTestBackend = false;
            _options = ValidateAndCopyOptions(options ?? new LoggerOptions());
            _consoleLoggingEnabled = _options.ConsoleLoggingEnabled;
            _testConsoleLoggingEnabled = _options.ConsoleLoggingEnabled;
            _testFileLoggingEnabled = false;
            _minimumConsoleLevel = _options.MinimumConsoleLevel;
            _minimumFileLevel = _options.MinimumFileLevel;
            _logFactory = new NLogFactory
            {
                AutoShutdown = false,
                ThrowExceptions = false,
                ThrowConfigExceptions = true
            };

            try
            {
                InitializeBackend();
                _nlogLogger = _logFactory.GetLogger(_options.LoggerName);
            }
            catch
            {
                _logFactory.Dispose();
                throw;
            }
        }

        internal Logger(LoggerOptions options, Action<PhysicalEmission> testObserver)
        {
            _testObserver = testObserver ?? throw new ArgumentNullException(nameof(testObserver));
            _usesTestBackend = true;
            _options = ValidateAndCopyOptions(options ?? new LoggerOptions());
            _consoleLoggingEnabled = _options.ConsoleLoggingEnabled;
            _testConsoleLoggingEnabled = _options.ConsoleLoggingEnabled;
            _testFileLoggingEnabled = false;
            _minimumConsoleLevel = _options.MinimumConsoleLevel;
            _minimumFileLevel = _options.MinimumFileLevel;
            _logFactory = new NLogFactory
            {
                AutoShutdown = false,
                ThrowExceptions = false,
                ThrowConfigExceptions = true
            };
            _nlogLogger = _logFactory.GetLogger(_options.LoggerName);
        }

        internal void SetTestRolloverProviders(Func<DateTime> currentDateProvider = null, Func<string, long> fileLengthProvider = null)
        {
            lock (_coordinatorSync)
            {
                ThrowIfNotActiveLocked();
                _currentDateProvider = currentDateProvider ?? (() => DateTime.Today);
                _fileLengthProvider = fileLengthProvider ?? TryGetFileLength;
            }
        }

        internal void SetTestFlushHook(Action hook)
        {
            lock (_coordinatorSync)
            {
                if (_lifecycleState != LoggerLifecycleState.Active)
                {
                    throw new ObjectDisposedException(nameof(Logger));
                }
                _testFlushHook = hook;
            }
        }

        internal void SetTestFileLoggingEnabled(bool enabled)
        {
            lock (_coordinatorSync)
            {
                ThrowIfNotActiveLocked();
                if (!_usesTestBackend)
                {
                    throw new InvalidOperationException("The test file-visibility switch is only available with the in-memory backend.");
                }

                if (_testFileLoggingEnabled == enabled)
                {
                    return;
                }

                CloseOpenPhysicalLineLocked(markInterrupted: true);
                _testFileLoggingEnabled = enabled;
            }
        }


        internal void SetTestDestinationVisibility(bool consoleEnabled, bool fileEnabled)
        {
            lock (_coordinatorSync)
            {
                ThrowIfNotActiveLocked();
                if (!_usesTestBackend)
                {
                    throw new InvalidOperationException("The test destination-visibility switch is only available with the in-memory backend.");
                }

                _testConsoleLoggingEnabled = consoleEnabled;
                _testFileLoggingEnabled = fileEnabled;
            }
        }

        internal void SetTestMinimumLevels(LogLevel consoleLevel, LogLevel fileLevel)
        {
            lock (_coordinatorSync)
            {
                ThrowIfNotActiveLocked();

                if (!Enum.IsDefined(typeof(LogLevel), consoleLevel))
                    throw new ArgumentOutOfRangeException(nameof(consoleLevel));

                if (!Enum.IsDefined(typeof(LogLevel), fileLevel))
                    throw new ArgumentOutOfRangeException(nameof(fileLevel));

                _minimumConsoleLevel = consoleLevel;
                _minimumFileLevel = fileLevel;
            }
        }

        internal void SetTestConfigurationApplyHook(Action<string> hook)
        {
            lock (_coordinatorSync)
            {
                ThrowIfNotActiveLocked();
                _testConfigurationApplyHook = hook;
            }
        }

        internal void DisposeEntryHandle(LogEntry entry)
        {
            lock (_coordinatorSync)
            {
                if (_lifecycleState != LoggerLifecycleState.Active)
                {
                    return;
                }

                if (!entry.BelongsTo(this) || !_activeEntries.Contains(entry) || !entry.IsActive)
                {
                    return;
                }

                CompleteEntryLocked(entry, message: null, exception: null, properties: null);
            }
        }

        /// <summary>Flushes targets, terminates any open physical line, completes active records without emitting repair text, releases destinations, and disposes the NLog backend.</summary>
        public void Dispose()
        {
            Exception failure = null;

            lock (_coordinatorSync)
            {
                if (_lifecycleState != LoggerLifecycleState.Active)
                {
                    return;
                }

                _lifecycleState = LoggerLifecycleState.Disposing;

                try
                {
                    try
                    {
                        CloseOpenPhysicalLineLocked(markInterrupted: false);
                    }
                    catch (Exception ex)
                    {
                        if (failure == null)
                        {
                            failure = ex;
                        }
                    }

                    foreach (var entry in _activeEntries)
                    {
                        entry.State = EntryLifecycleState.Completed;
                    }

                    _activeEntries.Clear();
                    _currentEntry.Value = null;
                    _openPhysicalEntry = null;

                    try
                    {
                        _testFlushHook?.Invoke();
                        _logFactory.Flush(_options.DisposeFlushTimeout);
                    }
                    catch (Exception ex)
                    {
                        if (failure == null)
                        {
                            failure = ex;
                        }
                    }
                }
                finally
                {
                    _fileLogPath = null;
                    _fileTarget = null;

                    try
                    {
                        _logFactory.Dispose();
                    }
                    catch (Exception ex)
                    {
                        if (failure == null)
                        {
                            failure = ex;
                        }
                    }
                    finally
                    {
                        _lifecycleState = LoggerLifecycleState.Disposed;
                    }
                }
            }

            GC.SuppressFinalize(this);

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        private static LoggerOptions ValidateAndCopyOptions(LoggerOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (string.IsNullOrWhiteSpace(options.LoggerName))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(options.LoggerName));

            if (string.IsNullOrWhiteSpace(options.TimestampFormat))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(options.TimestampFormat));

            try
            {
                _ = DateTime.Now.ToString(options.TimestampFormat, CultureInfo.InvariantCulture);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException(
                    "The timestamp format is not valid.",
                    nameof(options.TimestampFormat),
                    ex);
            }

            if (options.MessageSeparator == null)
                throw new ArgumentNullException(nameof(options.MessageSeparator));

            if (string.IsNullOrWhiteSpace(options.ConsoleFragmentLayout))
                throw new ArgumentException(
                    "Value cannot be null or whitespace.",
                    nameof(options.ConsoleFragmentLayout));

            if (string.IsNullOrWhiteSpace(options.FileFragmentLayout))
                throw new ArgumentException(
                    "Value cannot be null or whitespace.",
                    nameof(options.FileFragmentLayout));

            if (options.InlineResumePrefix == null)
                throw new ArgumentNullException(nameof(options.InlineResumePrefix));

            if (options.ArchivePolicy == null)
                throw new ArgumentNullException(nameof(options.ArchivePolicy));

            if (!Enum.IsDefined(typeof(LogLevel), options.MinimumConsoleLevel))
                throw new ArgumentOutOfRangeException(nameof(options.MinimumConsoleLevel));

            if (options.MinimumStandardErrorLevel is LogLevel standardErrorLevel &&
                !Enum.IsDefined(typeof(LogLevel), standardErrorLevel))
            {
                throw new ArgumentOutOfRangeException(nameof(options.MinimumStandardErrorLevel));
            }

            if (!Enum.IsDefined(typeof(LogLevel), options.MinimumFileLevel))
                throw new ArgumentOutOfRangeException(nameof(options.MinimumFileLevel));

            if (!Enum.IsDefined(typeof(LogTimestampZone), options.TimestampZone))
                throw new ArgumentOutOfRangeException(nameof(options.TimestampZone));

            if (options.EntryIndentSize < 0)
                throw new ArgumentOutOfRangeException(nameof(options.EntryIndentSize));

            if (options.DisposeFlushTimeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(options.DisposeFlushTimeout));

            ValidateOptionalLayout(options.ConsoleLayout, nameof(options.ConsoleLayout));
            ValidateOptionalLayout(options.FileLayout, nameof(options.FileLayout));

            switch (options.ArchivePolicy)
            {
                case FileArchivePolicy.BySize bySize when bySize.MaximumFileSizeBytes <= 0:
                    throw new ArgumentOutOfRangeException(
                        nameof(options.ArchivePolicy),
                        "Maximum file size must be positive.");

                case FileArchivePolicy.BySize bySize when bySize.MaximumArchiveFiles < UnlimitedArchiveFiles:
                    throw new ArgumentOutOfRangeException(
                        nameof(options.ArchivePolicy),
                        "MaximumArchiveFiles must be zero or greater, or Logger.UnlimitedArchiveFiles.");
            }

            return new LoggerOptions
            {
                LoggerName = options.LoggerName,
                ConsoleLoggingEnabled = options.ConsoleLoggingEnabled,
                MinimumConsoleLevel = options.MinimumConsoleLevel,
                MinimumStandardErrorLevel = options.MinimumStandardErrorLevel,
                MinimumFileLevel = options.MinimumFileLevel,
                IncludeConsoleTimestamp = options.IncludeConsoleTimestamp,
                IncludeConsoleLogLevel = options.IncludeConsoleLogLevel,
                IncludeFileTimestamp = options.IncludeFileTimestamp,
                IncludeFileLogLevel = options.IncludeFileLogLevel,
                IncludeFileEntryMetadata = options.IncludeFileEntryMetadata,
                TimestampFormat = options.TimestampFormat,
                TimestampZone = options.TimestampZone,
                MessageSeparator = options.MessageSeparator,
                ConsoleLayout = options.ConsoleLayout,
                ConsoleFragmentLayout = options.ConsoleFragmentLayout,
                FileLayout = options.FileLayout,
                FileFragmentLayout = options.FileFragmentLayout,
                InlineResumePrefix = options.InlineResumePrefix,
                EntryIndentSize = options.EntryIndentSize,
                DisposeFlushTimeout = options.DisposeFlushTimeout,
                ArchivePolicy = options.ArchivePolicy
            };
        }

        private static void ValidateOptionalLayout(string layout, string parameterName)
        {
            if (layout != null && string.IsNullOrWhiteSpace(layout))
            {
                throw new ArgumentException("A custom NLog layout cannot be empty or whitespace.", parameterName);
            }
        }
    }
}