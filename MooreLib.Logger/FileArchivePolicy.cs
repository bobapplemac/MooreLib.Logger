// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

namespace MooreLib.Logging;

/// <summary>Defines the archive and retention strategy used by the optional file target.</summary>
public abstract record FileArchivePolicy
{
    private FileArchivePolicy() { }

    /// <summary>Archives the active log file at a safe physical-line boundary after it reaches a configured size.</summary>
    /// <param name="MaximumFileSizeBytes">Approximate maximum active file size, in bytes, before rollover becomes required.</param>
    /// <param name="MaximumArchiveFiles">Maximum number of archived files to retain. Zero retains no archives; use <see cref="Logger.UnlimitedArchiveFiles"/> for no count limit.</param>
    public sealed record BySize(
        long MaximumFileSizeBytes = Logger.DefaultMaximumFileSizeBytes,
        int MaximumArchiveFiles = Logger.DefaultMaximumArchiveFiles)
        : FileArchivePolicy;

    /// <summary>Archives the active log file at the next safe physical-line boundary after the local date changes.</summary>
    /// <param name="MaximumArchiveDays">Maximum archive age in days. A value less than or equal to zero disables age-based deletion while retaining daily rollover.</param>
    public sealed record Daily(int MaximumArchiveDays)
        : FileArchivePolicy;
}
