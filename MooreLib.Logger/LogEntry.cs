// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;
using System.Threading;

namespace MooreLib.Logging;

/// <summary>
/// Represents a logical logger entry and provides deterministic, idempotent cleanup through <see cref="IDisposable"/>.
/// </summary>
/// <remarks>
/// Disposing an active handle completes the entry without writing additional text. Disposing an entry that was already
/// completed explicitly is a no-op. No finalizer is used; callers that want deterministic cleanup should use <c>using</c>.
/// </remarks>
public sealed class LogEntry : IDisposable
{
    private Logger? _owner;

    internal LogEntry(Logger owner, long id)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Id = id;
    }

    /// <summary>Gets the unique identifier assigned to the logical entry.</summary>
    public long Id { get; }

    /// <summary>Returns the numeric entry identifier so existing explicit-ID APIs remain convenient.</summary>
    /// <param name="entry">Entry handle to convert.</param>
    public static implicit operator long(LogEntry entry) =>
        entry?.Id ?? throw new ArgumentNullException(nameof(entry));

    /// <summary>Completes the entry if it is still active. Repeated disposal is harmless.</summary>
    public void Dispose()
    {
        var owner = Interlocked.Exchange(ref _owner, null);
        owner?.DisposeEntryHandle(Id);
    }

    /// <inheritdoc/>
    public override string ToString() => Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
