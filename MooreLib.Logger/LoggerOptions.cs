// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;

namespace MooreLib.Logging
{
    /// <summary>Specifies which time zone is used when rendering generated timestamps.</summary>
    public enum LogTimestampZone
    {
        /// <summary>Render timestamps using the local system time zone.</summary>
        Local,
        /// <summary>Render timestamps using Coordinated Universal Time (UTC).</summary>
        Utc
    }

    /// <summary>Configures console/file rendering, filtering, indentation, archival behavior, and shutdown flushing for <see cref="Logger"/>.</summary>
    /// <remarks>Options are copied and validated when a <see cref="Logger"/> instance is constructed.</remarks>
    public sealed class LoggerOptions
    {
        /// <summary>Default timestamp format used by generated line layouts.</summary>
        public const string DefaultTimestampFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>Default NLog layout used for physical fragments that must not render a header.</summary>
        public const string DefaultFragmentLayout = "${message}";

        /// <summary>NLog logger name used by this wrapper instance.</summary>
        public string LoggerName { get; set; } = "MooreLib.Logger";

        /// <summary>Whether console logging is enabled when the logger is constructed.</summary>
        public bool ConsoleLoggingEnabled { get; set; } = true;

        /// <summary>Minimum severity emitted to the console when console logging is enabled.</summary>
        public LogLevel MinimumConsoleLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// Minimum console severity routed to standard error. Lower visible console severities are routed to standard output.
        /// Set to <see langword="null"/> to route all console output to standard output.
        /// </summary>
        public LogLevel? MinimumStandardErrorLevel { get; set; } = LogLevel.Error;

        /// <summary>Minimum severity emitted to the optional file target.</summary>
        public LogLevel MinimumFileLevel { get; set; } = LogLevel.Debug;

        /// <summary>Whether generated console headers include a timestamp.</summary>
        public bool IncludeConsoleTimestamp { get; set; }

        /// <summary>Whether generated console headers include the log-level label.</summary>
        public bool IncludeConsoleLogLevel { get; set; } = true;

        /// <summary>Whether generated file headers include a timestamp.</summary>
        public bool IncludeFileTimestamp { get; set; } = true;

        /// <summary>Whether generated file headers include the log-level label.</summary>
        public bool IncludeFileLogLevel { get; set; } = true;

        /// <summary>Whether MooreLib-reserved entry metadata is rendered in the human-readable file layout.</summary>
        public bool IncludeFileEntryMetadata { get; set; } = true;

        /// <summary>Date/time format string used by generated timestamp renderers.</summary>
        public string TimestampFormat { get; set; } = DefaultTimestampFormat;

        /// <summary>Time zone used by generated timestamp renderers.</summary>
        public LogTimestampZone TimestampZone { get; set; } = LogTimestampZone.Local;

        /// <summary>Text inserted between a generated non-empty header prefix and a non-empty message.</summary>
        public string MessageSeparator { get; set; } = " ";

        /// <summary>Optional complete NLog layout override for header-bearing console lines.</summary>
        public string ConsoleLayout { get; set; }

        /// <summary>NLog layout used for console fragments that must not repeat the normal line header.</summary>
        public string ConsoleFragmentLayout { get; set; } = DefaultFragmentLayout;

        /// <summary>Optional complete NLog layout override for header-bearing file lines.</summary>
        public string FileLayout { get; set; }

        /// <summary>NLog layout used for file fragments that must not repeat the normal line header.</summary>
        public string FileFragmentLayout { get; set; } = DefaultFragmentLayout;

        /// <summary>Prefix used when a previously interrupted inline entry resumes on a new physical line.</summary>
        public string InlineResumePrefix { get; set; } = "↳ ";

        /// <summary>Width, in characters, reserved for each nested tree ancestry column.</summary>
        public int EntryIndentSize { get; set; } = 2;

        /// <summary>Maximum time allowed for NLog targets to flush during logger disposal.</summary>
        public TimeSpan DisposeFlushTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>Archive policy applied when file logging is enabled.</summary>
        public FileArchivePolicy ArchivePolicy { get; set; } = new FileArchivePolicy.BySize();
    }
}