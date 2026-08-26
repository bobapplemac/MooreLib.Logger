// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;
using System.IO;

namespace MooreLib.Logging;

public sealed partial class Logger
{
    /// <summary>Enables or reconfigures immediate file logging at the supplied active file path.</summary>
    /// <param name="filePath">Active log file path. Relative paths are normalized to a full path.</param>
    /// <remarks>
    /// Configuration changes are transactional. A prospective destination is claimed and configured
    /// before MooreLib commits its own file-target state. If preparation or application fails, the
    /// previous working configuration and ownership remain intact.
    /// </remarks>
    public void EnableFileLogging(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
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
            var prepared = PrepareConfigurationLocked(normalized);
            var prospectivePath = DestinationOwnershipRegistry.AcquireFile(_ownerId, normalized);
            var oldPath = _fileLogPath;

            try
            {
                CloseOpenPhysicalLineLocked(markInterrupted: true);
                _logFactory.Flush(_options.DisposeFlushTimeout);
                ApplyPreparedConfigurationLocked(prepared);
                CommitPreparedConfigurationLocked(prepared);
            }
            catch
            {
                DestinationOwnershipRegistry.ReleaseFile(_ownerId, prospectivePath);
                throw;
            }

            // The new configuration is now active. Only now may the old destination be released.
            if (!PathsEqual(oldPath, prospectivePath))
            {
                DestinationOwnershipRegistry.ReleaseFile(_ownerId, oldPath);
            }
        }
    }

    /// <summary>Disables file logging while preserving console logging and allowing interrupted logical entries to resume later.</summary>
    /// <remarks>
    /// Console-only configuration is applied successfully before the previous file destination is
    /// released. A failed disable therefore leaves the previous file configuration and ownership active.
    /// </remarks>
    public void DisableFileLogging()
    {
        lock (_coordinatorSync)
        {
            ThrowIfNotActiveLocked();
            if (_usesTestBackend)
                throw new NotSupportedException("File logging is not available through the in-memory test backend.");
            if (_fileLogPath is null)
            {
                return;
            }

            var oldPath = _fileLogPath;
            var prepared = PrepareConfigurationLocked(filePath: null);

            CloseOpenPhysicalLineLocked(markInterrupted: true);
            _logFactory.Flush(_options.DisposeFlushTimeout);
            ApplyPreparedConfigurationLocked(prepared);
            CommitPreparedConfigurationLocked(prepared);

            // Release only after console-only NLog configuration and MooreLib state commit.
            DestinationOwnershipRegistry.ReleaseFile(_ownerId, oldPath);
        }
    }

    private static bool PathsEqual(string? left, string right)
    {
        if (left is null) return false;
        return OperatingSystem.IsWindows()
            ? string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
            : string.Equals(left, right, StringComparison.Ordinal);
    }
}
