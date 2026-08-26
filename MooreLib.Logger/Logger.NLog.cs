// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;
using System.Globalization;
using System.IO;
using System.Text;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using NLogFactory = NLog.LogFactory;
using NLogLevel = NLog.LogLevel;
using NLogLogger = NLog.Logger;

namespace MooreLib.Logging;

public sealed partial class Logger
{
    private static readonly Layout ExceptionTextLayout =
        Layout.FromString("${exception:format=tostring}");

    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly NLogFactory _logFactory;
    private readonly NLogLogger _nlogLogger;
    private FileTarget? _fileTarget;
    private string? _fileLogPath;
    private DateTime _activeFileDate;
    private bool _rolloverRequired;
    private bool _forceArchiveOnNextEligibleWrite;
    private Func<DateTime> _currentDateProvider = static () => DateTime.Today;
    private Func<string, long> _fileLengthProvider = TryGetFileLength;

    private sealed record PreparedConfiguration(
        LoggingConfiguration Configuration,
        bool ConsoleLoggingEnabled,
        FileTarget? FileTarget,
        string? FilePath,
        DateTime ActiveFileDate,
        bool RolloverRequired,
        bool ForceArchiveOnNextEligibleWrite);

    private void InitializeBackend()
    {
        var prepared = PrepareConfigurationLocked(filePath: null, consoleLoggingEnabled: _consoleLoggingEnabled);
        ApplyPreparedConfigurationLocked(prepared);
        CommitPreparedConfigurationLocked(prepared);
    }

    private PreparedConfiguration PrepareConfigurationLocked(string? filePath, bool consoleLoggingEnabled)
    {
        var configuration = new LoggingConfiguration(_logFactory);

        var consoleLineLayout = CreateLineLayout(
            _options.ConsoleLayout,
            _options.IncludeConsoleTimestamp,
            _options.IncludeConsoleLogLevel,
            includeEventProperties: false,
            includeEntryMetadata: false);

        if (consoleLoggingEnabled)
        {
            var stdoutTarget = new ExactConsoleTarget("stdout", useStandardError: false)
            {
                Layout = new PhysicalOutputLayout(consoleLineLayout, _options.ConsoleFragmentLayout),
                AutoFlush = true
            };

            var stderrTarget = new ExactConsoleTarget("stderr", useStandardError: true)
            {
                Layout = new PhysicalOutputLayout(consoleLineLayout, _options.ConsoleFragmentLayout),
                AutoFlush = true
            };

            AddConsoleRules(configuration, stdoutTarget, stderrTarget);
        }

        FileTarget? fileTarget = null;
        var activeFileDate = _currentDateProvider().Date;

        if (filePath is not null)
        {
            var fileLineLayout = CreateLineLayout(
                _options.FileLayout,
                _options.IncludeFileTimestamp,
                _options.IncludeFileLogLevel,
                includeEventProperties: true,
                includeEntryMetadata: _options.IncludeFileEntryMetadata);

            fileTarget = new FileTarget("file")
            {
                FileName = filePath,
                ArchiveFileName = filePath,
                Layout = new PhysicalOutputLayout(fileLineLayout, _options.FileFragmentLayout),
                LineEnding = LineEndingMode.None,
                CreateDirs = true,
                KeepFileOpen = true,
                AutoFlush = true,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                WriteBom = false,
                ArchiveAboveSize = 0,
                ArchiveEvery = FileArchivePeriod.None,
                ArchiveSuffixFormat = "_{1:yyyyMMdd}_{0:00}"
            };

            ConfigureRetention(fileTarget, _options.ArchivePolicy);
            configuration.AddRule(ToNLogLevel(_minimumFileLevel), NLogLevel.Fatal, fileTarget);
            activeFileDate = GetExistingFileDate(filePath, _currentDateProvider().Date);
        }

        return new PreparedConfiguration(
            configuration,
            consoleLoggingEnabled,
            fileTarget,
            filePath,
            activeFileDate,
            RolloverRequired: false,
            ForceArchiveOnNextEligibleWrite: false);
    }

    private void ApplyPreparedConfigurationLocked(PreparedConfiguration prepared)
    {
        _testConfigurationApplyHook?.Invoke(prepared.FilePath);

        var previous = _logFactory.Configuration;
        try
        {
            _logFactory.Configuration = prepared.Configuration;
        }
        catch
        {
            if (previous is not null && !ReferenceEquals(_logFactory.Configuration, previous))
            {
                try
                {
                    _logFactory.Configuration = previous;
                }
                catch
                {
                    // Preserve the original configuration exception. MooreLib fields are intentionally
                    // not committed unless the prospective configuration succeeds.
                }
            }
            throw;
        }
    }

    private void CommitPreparedConfigurationLocked(PreparedConfiguration prepared)
    {
        _consoleLoggingEnabled = prepared.ConsoleLoggingEnabled;
        _fileTarget = prepared.FileTarget;
        _fileLogPath = prepared.FilePath;
        _activeFileDate = prepared.ActiveFileDate;
        _rolloverRequired = prepared.RolloverRequired;
        _forceArchiveOnNextEligibleWrite = prepared.ForceArchiveOnNextEligibleWrite;
    }

    private void AddConsoleRules(
        LoggingConfiguration configuration,
        ExactConsoleTarget stdoutTarget,
        ExactConsoleTarget stderrTarget)
    {
        var standardErrorMinimum = _options.MinimumStandardErrorLevel;

        if (standardErrorMinimum is null)
        {
            configuration.AddRule(
                ToNLogLevel(_minimumConsoleLevel),
                NLogLevel.Fatal,
                stdoutTarget);
            return;
        }

        var stderrMinimum = (int)_minimumConsoleLevel > (int)standardErrorMinimum.Value
            ? _minimumConsoleLevel
            : standardErrorMinimum.Value;

        var stdoutMaximumValue = (int)standardErrorMinimum.Value - 1;
        if ((int)_minimumConsoleLevel <= stdoutMaximumValue)
        {
            configuration.AddRule(
                ToNLogLevel(_minimumConsoleLevel),
                ToNLogLevel((LogLevel)stdoutMaximumValue),
                stdoutTarget);
        }

        configuration.AddRule(
            ToNLogLevel(stderrMinimum),
            NLogLevel.Fatal,
            stderrTarget);
    }

    private Layout CreateLineLayout(
        string? layoutOverride,
        bool includeTimestamp,
        bool includeLogLevel,
        bool includeEventProperties,
        bool includeEntryMetadata)
    {
        if (layoutOverride is not null)
        {
            return Layout.FromString(layoutOverride);
        }

        return new StandardLineLayout(
            includeTimestamp,
            includeLogLevel,
            includeEventProperties,
            includeEntryMetadata,
            _options.TimestampFormat,
            _options.TimestampZone,
            _options.MessageSeparator);
    }

    private static void ConfigureRetention(FileTarget fileTarget, FileArchivePolicy archivePolicy)
    {
        switch (archivePolicy)
        {
            case FileArchivePolicy.BySize bySize:
                fileTarget.MaxArchiveFiles = bySize.MaximumArchiveFiles;
                fileTarget.MaxArchiveDays = 0;
                break;

            case FileArchivePolicy.Daily daily:
                fileTarget.MaxArchiveFiles = UnlimitedArchiveFiles;
                fileTarget.MaxArchiveDays = Math.Max(0, daily.MaximumArchiveDays);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(archivePolicy));
        }
    }

    private void LogPhysicalLocked(PhysicalEvent logEvent, LogLevel level, bool startsNewPhysicalLine)
    {
        if (startsNewPhysicalLine)
        {
            EvaluateRolloverRequirementLocked(level);
        }

        var fileEligible = _fileTarget is not null && (int)level >= (int)_minimumFileLevel;
        if (_forceArchiveOnNextEligibleWrite && fileEligible && _fileTarget is not null)
        {
            _fileTarget.ArchiveAboveSize = 1;
        }

        try
        {
            _nlogLogger.Log(typeof(Logger), logEvent);
        }
        finally
        {
            if (_forceArchiveOnNextEligibleWrite && fileEligible && _fileTarget is not null)
            {
                _fileTarget.ArchiveAboveSize = 0;
                _forceArchiveOnNextEligibleWrite = false;
                _rolloverRequired = false;
                _activeFileDate = _currentDateProvider().Date;
            }
        }
    }

    private void EvaluateRolloverRequirementLocked(LogLevel upcomingLevel)
    {
        if (_fileTarget is null || _fileLogPath is null || (int)upcomingLevel < (int)_minimumFileLevel)
        {
            return;
        }

        if (!_rolloverRequired)
        {
            _rolloverRequired = _options.ArchivePolicy switch
            {
                FileArchivePolicy.BySize bySize =>
                    _fileLengthProvider(_fileLogPath) >= bySize.MaximumFileSizeBytes,
                FileArchivePolicy.Daily => _currentDateProvider().Date > _activeFileDate,
                _ => false
            };
        }

        if (!_rolloverRequired)
        {
            return;
        }

        var length = _fileLengthProvider(_fileLogPath);
        if (length <= 0)
        {
            _rolloverRequired = false;
            _activeFileDate = _currentDateProvider().Date;
            return;
        }

        _forceArchiveOnNextEligibleWrite = true;
    }

    private string[] RenderExceptionLines(LogLevel level, Exception exception)
    {
        var probe = new NLog.LogEventInfo(ToNLogLevel(level), _options.LoggerName, string.Empty)
        {
            Exception = exception
        };
        var rendered = ExceptionTextLayout.Render(probe);
        return SplitPhysicalLines(rendered);
    }

    private static long TryGetFileLength(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static DateTime GetExistingFileDate(string path, DateTime fallbackDate)
    {
        try
        {
            return File.Exists(path)
                ? File.GetLastWriteTime(path).Date
                : fallbackDate.Date;
        }
        catch (IOException)
        {
            return fallbackDate.Date;
        }
        catch (UnauthorizedAccessException)
        {
            return fallbackDate.Date;
        }
    }

    private static NLogLevel ToNLogLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => NLogLevel.Trace,
        LogLevel.Debug => NLogLevel.Debug,
        LogLevel.Info => NLogLevel.Info,
        LogLevel.Warning => NLogLevel.Warn,
        LogLevel.Error => NLogLevel.Error,
        LogLevel.Fatal => NLogLevel.Fatal,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unsupported log level.")
    };
}
