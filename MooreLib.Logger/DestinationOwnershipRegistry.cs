// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;
using System.Collections.Generic;
using System.IO;

namespace MooreLib.Logging;

internal static class DestinationOwnershipRegistry
{
    private static readonly object Sync = new();
    private static readonly HashSet<Guid> ConsoleOwners = new();
    private static readonly Dictionary<string, Guid> FileOwners = new(PathComparer);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static void AcquireConsole(Guid ownerId)
    {
        lock (Sync)
        {
            if (ConsoleOwners.Count != 0 && !ConsoleOwners.Contains(ownerId))
            {
                throw new InvalidOperationException(
                    "The process console is already owned by another active MooreLib.Logger instance.");
            }

            ConsoleOwners.Add(ownerId);
        }
    }

    public static void ReleaseConsole(Guid ownerId)
    {
        lock (Sync)
        {
            ConsoleOwners.Remove(ownerId);
        }
    }

    public static string AcquireFile(Guid ownerId, string path)
    {
        var normalized = Path.GetFullPath(path);

        lock (Sync)
        {
            if (FileOwners.TryGetValue(normalized, out var existingOwner) && existingOwner != ownerId)
            {
                throw new InvalidOperationException(
                    $"The log file '{normalized}' is already owned by another active MooreLib.Logger instance.");
            }

            FileOwners[normalized] = ownerId;
            return normalized;
        }
    }

    public static void ReleaseFile(Guid ownerId, string? path)
    {
        if (path is null)
        {
            return;
        }

        var normalized = Path.GetFullPath(path);
        lock (Sync)
        {
            if (FileOwners.TryGetValue(normalized, out var existingOwner) && existingOwner == ownerId)
            {
                FileOwners.Remove(normalized);
            }
        }
    }
}
