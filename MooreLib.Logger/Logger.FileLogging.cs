// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;
using System.IO;

namespace MooreLib.Logging
{
    public sealed partial class Logger
    {
        /// <summary>Enables or reconfigures immediate file logging at the supplied active file path.</summary>
        /// <param name="filePath">Active log file path. Relative paths are normalized to a full path.</param>
        /// <remarks>
        /// Configuration changes are transactional. A prospective NLog configuration is prepared and applied
        /// before MooreLib commits its own file-target state. If preparation or application fails, the
        /// previous working configuration remains intact.
        /// </remarks>
        public void EnableFileLogging(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));

            var normalized = Path.GetFullPath(filePath);

            lock (_coordinatorSync)
            {
                ThrowIfNotActiveLocked();
                if (_usesTestBackend)
                    throw new NotSupportedException("File logging is not available through the in-memory test backend.");
                if (PathsEqual(_fileLogPath, normalized))
                {
                    return;
                }

                // Prepare first without mutating active MooreLib/NLog state.
                var prepared = PrepareConfigurationLocked(normalized, _consoleLoggingEnabled);

                CloseOpenPhysicalLineLocked(markInterrupted: true);
                _logFactory.Flush(_options.DisposeFlushTimeout);
                ApplyPreparedConfigurationLocked(prepared);
                CommitPreparedConfigurationLocked(prepared);
            }
        }

        /// <summary>Disables file logging while preserving the current console destination state and allowing interrupted logical entries to resume later.</summary>
        /// <remarks>
        /// A configuration without the file target is applied successfully before MooreLib commits the disabled file state.
        /// A failed disable therefore leaves the previous file configuration active.
        /// </remarks>
        public void DisableFileLogging()
        {
            lock (_coordinatorSync)
            {
                ThrowIfNotActiveLocked();
                if (_usesTestBackend)
                    throw new NotSupportedException("File logging is not available through the in-memory test backend.");
                if (_fileLogPath == null)
                {
                    return;
                }

                var prepared = PrepareConfigurationLocked(filePath: null, consoleLoggingEnabled: _consoleLoggingEnabled);

                CloseOpenPhysicalLineLocked(markInterrupted: true);
                _logFactory.Flush(_options.DisposeFlushTimeout);
                ApplyPreparedConfigurationLocked(prepared);
                CommitPreparedConfigurationLocked(prepared);
            }
        }

        // Prefer the modern platform API when available. .NET Framework 4.6.2 does not
        // provide OperatingSystem.IsWindows(), so the legacy target falls back to
        // Environment.OSVersion.Platform.
        private static bool IsWindows()
        {
#if NET8_0_OR_GREATER
            return OperatingSystem.IsWindows();
#else
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32Windows:
                case PlatformID.Win32S:
                case PlatformID.WinCE:
                    return true;

                default:
                    return false;
            }
#endif
        }

        private static bool PathsEqual(string left, string right)
        {
            if (left == null)
            {
                return false;
            }

            return IsWindows()
                ? string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
                : string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}