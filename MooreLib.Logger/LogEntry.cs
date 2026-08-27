// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;
using System.Threading;
using System.Globalization;

namespace MooreLib.Logging
{
    /// <summary>
    /// Represents a logical logger entry and provides deterministic, idempotent cleanup through <see cref="IDisposable"/>.
    /// </summary>
    /// <remarks>
    /// A <see cref="LogEntry"/> is both the public handle for explicit entry targeting and the logger's internal
    /// per-entry state object. Mutable state remains controlled exclusively by the owning <see cref="Logger"/>
    /// under its coordinator lock; callers cannot mutate entry state directly.
    /// </remarks>
    public sealed class LogEntry : IDisposable
    {
        private readonly Logger _originOwner;
        private Logger _disposalOwner;

        internal LogEntry(
            Logger owner,
            long entrySequence,
            LogEntry parent,
            LogLevel level,
            LogProperty[] properties)
        {
            _originOwner = owner ?? throw new ArgumentNullException(nameof(owner));
            _disposalOwner = owner;
            EntrySequence = entrySequence;
            Parent = parent;
            Level = level;
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
            Depth = parent == null ? 0 : checked(parent.Depth + 1);
            State = Logger.EntryLifecycleState.ActiveLineClosed;
        }

        /// <summary>
        /// Gets the logger-instance-scoped sequence number assigned to this entry.
        /// </summary>
        /// <remarks>
        /// The value is intended for diagnostics and correlation only. Explicit logger APIs use the
        /// <see cref="LogEntry"/> object itself rather than resolving entries by sequence number.
        /// </remarks>
        public long EntrySequence { get; }

        internal LogEntry Parent { get; }
        internal LogLevel Level { get; }
        internal LogProperty[] Properties { get; }
        internal int Depth { get; }
        internal Logger.EntryLifecycleState State { get; set; }
        internal bool HasVisibleTreeContent { get; set; }

        internal bool IsActive
        {
            get { return State != Logger.EntryLifecycleState.Completed; }
        }

        internal bool OwnsOpenLine
        {
            get
            {
                return State == Logger.EntryLifecycleState.ActiveLineOpen ||
                       State == Logger.EntryLifecycleState.CompletingLineOpen;
            }
        }

        internal bool NeedsResume
        {
            get
            {
                return State == Logger.EntryLifecycleState.ActiveInterrupted ||
                       State == Logger.EntryLifecycleState.CompletingInterrupted;
            }
        }

        internal bool IsCompleting
        {
            get
            {
                return State == Logger.EntryLifecycleState.CompletingLinePending ||
                       State == Logger.EntryLifecycleState.CompletingLineOpen ||
                       State == Logger.EntryLifecycleState.CompletingInterrupted;
            }
        }

        internal bool BelongsTo(Logger owner)
        {
            return ReferenceEquals(_originOwner, owner);
        }

        /// <summary>Completes the entry if it is still active. Repeated disposal is harmless.</summary>
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _disposalOwner, null);

            if (owner != null)
            {
                owner.DisposeEntryHandle(this);
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return EntrySequence.ToString(CultureInfo.InvariantCulture);
        }
    }
}