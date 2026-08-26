// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;
using System.Threading;

namespace MooreLib.Logging;

/// <summary>
/// Represents a logical logger entry and provides deterministic, idempotent cleanup through <see cref="IDisposable"/>.
/// </summary>
/// <remarks>
/// Disposing an active handle performs a message-less completion. If visible tree content needs explicit visual closure,
/// that completion may emit the configured tree-closure marker. Disposing an entry that was already completed explicitly is a no-op. No finalizer is used; callers that want deterministic cleanup should use <c>using</c>.
/// </remarks>
public sealed class LogEntry : IDisposable
{
    private readonly Logger _originOwner;
    private Logger? _owner;

    internal LogEntry(Logger owner, long id)
    {
        _originOwner = owner ?? throw new ArgumentNullException(nameof(owner));
        _owner = owner;
        Id = id;
    }

    /// <summary>Gets the unique identifier assigned to the logical entry.</summary>
    public long Id { get; }

    internal bool BelongsTo(Logger owner) => ReferenceEquals(_originOwner, owner);

    /// <summary>Completes the entry if it is still active. Repeated disposal is harmless.</summary>
    public void Dispose()
    {
        var owner = Interlocked.Exchange(ref _owner, null);
        owner?.DisposeEntryHandle(Id);
    }

    /// <inheritdoc/>
    public override string ToString() => Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
