// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

namespace MooreLib.Logging;

/// <summary>Specifies the severity assigned to a log event or logical entry.</summary>
public enum LogLevel
{
    /// <summary>Very detailed diagnostic information, normally disabled outside deep troubleshooting.</summary>
    Trace,
    /// <summary>Diagnostic information intended primarily for troubleshooting.</summary>
    Debug,
    /// <summary>Informational application activity.</summary>
    Info,
    /// <summary>A recoverable or noteworthy condition that may require attention.</summary>
    Warning,
    /// <summary>An error condition that prevented an operation from completing normally.</summary>
    Error,
    /// <summary>A severe error condition after which continued operation may not be possible.</summary>
    Fatal
}
