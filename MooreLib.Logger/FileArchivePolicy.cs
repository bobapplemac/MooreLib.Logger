// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;

namespace MooreLib.Logging
{
    /// <summary>Defines the archive and retention strategy used by the optional file target.</summary>
    public abstract class FileArchivePolicy
    {
        private FileArchivePolicy()
        {
        }

        /// <summary>
        /// Archives the active log file at a safe physical-line boundary after it reaches a configured size.
        /// </summary>
        public sealed class BySize : FileArchivePolicy, IEquatable<BySize>
        {
            /// <summary>
            /// Gets the approximate maximum active file size, in bytes, before rollover becomes required.
            /// </summary>
            public long MaximumFileSizeBytes { get; }

            /// <summary>
            /// Gets the maximum number of archived files to retain.
            /// Zero retains no archives; use <see cref="Logger.UnlimitedArchiveFiles"/> for no count limit.
            /// </summary>
            public int MaximumArchiveFiles { get; }

            /// <summary>
            /// Initializes a new size-based archive policy.
            /// </summary>
            /// <param name="maximumFileSizeBytes">
            /// Approximate maximum active file size, in bytes, before rollover becomes required.
            /// </param>
            /// <param name="maximumArchiveFiles">
            /// Maximum number of archived files to retain.
            /// Zero retains no archives; use <see cref="Logger.UnlimitedArchiveFiles"/> for no count limit.
            /// </param>
            public BySize(
                long maximumFileSizeBytes = Logger.DefaultMaximumFileSizeBytes,
                int maximumArchiveFiles = Logger.DefaultMaximumArchiveFiles)
            {
                MaximumFileSizeBytes = maximumFileSizeBytes;
                MaximumArchiveFiles = maximumArchiveFiles;
            }

            /// <summary>
            /// Determines whether the specified size-based archive policy has the same values as this instance.
            /// </summary>
            /// <param name="other">The policy to compare with this instance.</param>
            /// <returns><see langword="true"/> if the policies have equal values; otherwise, <see langword="false"/>.</returns>
            public bool Equals(BySize other)
            {
                return !ReferenceEquals(other, null) &&
                       MaximumFileSizeBytes == other.MaximumFileSizeBytes &&
                       MaximumArchiveFiles == other.MaximumArchiveFiles;
            }

            /// <inheritdoc/>
            public override bool Equals(object obj)
            {
                return Equals(obj as BySize);
            }

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = 17;
                    hashCode = (hashCode * 31) + MaximumFileSizeBytes.GetHashCode();
                    hashCode = (hashCode * 31) + MaximumArchiveFiles.GetHashCode();
                    return hashCode;
                }
            }

            /// <summary>Determines whether two size-based archive policies have equal values.</summary>
            public static bool operator ==(BySize left, BySize right)
            {
                if (ReferenceEquals(left, right))
                    return true;

                if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                    return false;

                return left.Equals(right);
            }

            /// <summary>Determines whether two size-based archive policies have different values.</summary>
            public static bool operator !=(BySize left, BySize right)
            {
                return !(left == right);
            }
        }

        /// <summary>
        /// Archives the active log file at the next safe physical-line boundary after the local date changes.
        /// </summary>
        public sealed class Daily : FileArchivePolicy, IEquatable<Daily>
        {
            /// <summary>
            /// Gets the maximum archive age in days.
            /// A value less than or equal to zero disables age-based deletion while retaining daily rollover.
            /// </summary>
            public int MaximumArchiveDays { get; }

            /// <summary>
            /// Initializes a new daily archive policy.
            /// </summary>
            /// <param name="maximumArchiveDays">
            /// Maximum archive age in days.
            /// A value less than or equal to zero disables age-based deletion while retaining daily rollover.
            /// </param>
            public Daily(int maximumArchiveDays)
            {
                MaximumArchiveDays = maximumArchiveDays;
            }

            /// <summary>
            /// Determines whether the specified daily archive policy has the same value as this instance.
            /// </summary>
            /// <param name="other">The policy to compare with this instance.</param>
            /// <returns><see langword="true"/> if the policies have equal values; otherwise, <see langword="false"/>.</returns>
            public bool Equals(Daily other)
            {
                return !ReferenceEquals(other, null) &&
                       MaximumArchiveDays == other.MaximumArchiveDays;
            }

            /// <inheritdoc/>
            public override bool Equals(object obj)
            {
                return Equals(obj as Daily);
            }

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                return MaximumArchiveDays.GetHashCode();
            }

            /// <summary>Determines whether two daily archive policies have equal values.</summary>
            public static bool operator ==(Daily left, Daily right)
            {
                if (ReferenceEquals(left, right))
                    return true;

                if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                    return false;

                return left.Equals(right);
            }

            /// <summary>Determines whether two daily archive policies have different values.</summary>
            public static bool operator !=(Daily left, Daily right)
            {
                return !(left == right);
            }
        }
    }
}