// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

namespace MooreLib.Logging
{
    public sealed partial class Logger
    {
        /// <summary>Enables console logging while preserving the current file destination state.</summary>
        /// <remarks>
        /// The change is prospective: previously suppressed content is not replayed. If an inline physical line
        /// is currently open, it is terminated against the old destination set before console logging is enabled.
        /// </remarks>
        public void EnableConsoleLogging()
        {
            lock (_coordinatorSync)
            {
                ThrowIfNotActiveLocked();

                if (IsConsoleDestinationEnabledLocked())
                {
                    return;
                }

                if (_usesTestBackend)
                {
                    CloseOpenPhysicalLineLocked(markInterrupted: true);
                    _testConsoleLoggingEnabled = true;
                    return;
                }

                var prepared = PrepareConfigurationLocked(_fileLogPath, consoleLoggingEnabled: true);

                CloseOpenPhysicalLineLocked(markInterrupted: true);
                _logFactory.Flush(_options.DisposeFlushTimeout);
                ApplyPreparedConfigurationLocked(prepared);
                CommitPreparedConfigurationLocked(prepared);
            }
        }

        /// <summary>Disables console logging while preserving the current file destination state.</summary>
        /// <remarks>
        /// The change is prospective. If an inline physical line is currently open, it is terminated against
        /// the old destination set before console logging is disabled.
        /// </remarks>
        public void DisableConsoleLogging()
        {
            lock (_coordinatorSync)
            {
                ThrowIfNotActiveLocked();

                if (!IsConsoleDestinationEnabledLocked())
                {
                    return;
                }

                if (_usesTestBackend)
                {
                    CloseOpenPhysicalLineLocked(markInterrupted: true);
                    _testConsoleLoggingEnabled = false;
                    return;
                }

                var prepared = PrepareConfigurationLocked(_fileLogPath, consoleLoggingEnabled: false);

                CloseOpenPhysicalLineLocked(markInterrupted: true);
                _logFactory.Flush(_options.DisposeFlushTimeout);
                ApplyPreparedConfigurationLocked(prepared);
                CommitPreparedConfigurationLocked(prepared);
            }
        }
    }
}