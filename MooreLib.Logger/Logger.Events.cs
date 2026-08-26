// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;

namespace MooreLib.Logging;

public sealed partial class Logger
{
    /// <summary>Writes a Trace-level one-shot event.</summary>
    /// <param name="message">Message text. Embedded CR, LF, and CRLF sequences are emitted as physical continuation lines.</param>
    /// <param name="properties">Structured properties attached to the generated NLog events.</param>
    public void Trace(string message, params LogProperty[] properties) => WriteEvent(LogLevel.Trace, message, properties);

    /// <summary>Writes a Debug-level one-shot event.</summary>
    /// <param name="message">Message text. Embedded CR, LF, and CRLF sequences are emitted as physical continuation lines.</param>
    /// <param name="properties">Structured properties attached to the generated NLog events.</param>
    public void Debug(string message, params LogProperty[] properties) => WriteEvent(LogLevel.Debug, message, properties);

    /// <summary>Writes a Info-level one-shot event.</summary>
    /// <param name="message">Message text. Embedded CR, LF, and CRLF sequences are emitted as physical continuation lines.</param>
    /// <param name="properties">Structured properties attached to the generated NLog events.</param>
    public void Info(string message, params LogProperty[] properties) => WriteEvent(LogLevel.Info, message, properties);

    /// <summary>Writes a Warning-level one-shot event.</summary>
    /// <param name="message">Message text. Embedded CR, LF, and CRLF sequences are emitted as physical continuation lines.</param>
    /// <param name="properties">Structured properties attached to the generated NLog events.</param>
    public void Warn(string message, params LogProperty[] properties) => WriteEvent(LogLevel.Warning, message, properties);

    /// <summary>Writes a Error-level one-shot event.</summary>
    /// <param name="message">Message text. Embedded CR, LF, and CRLF sequences are emitted as physical continuation lines.</param>
    /// <param name="properties">Structured properties attached to the generated NLog events.</param>
    public void Error(string message, params LogProperty[] properties) => WriteEvent(LogLevel.Error, message, properties);

    /// <summary>Writes a Fatal-level one-shot event.</summary>
    /// <param name="message">Message text. Embedded CR, LF, and CRLF sequences are emitted as physical continuation lines.</param>
    /// <param name="properties">Structured properties attached to the generated NLog events.</param>
    public void Fatal(string message, params LogProperty[] properties) => WriteEvent(LogLevel.Fatal, message, properties);

    /// <summary>Writes a Trace-level one-shot event with an attached exception.</summary>
    /// <param name="message">Message text rendered before the exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event and also rendered through MooreLib continuation formatting.</param>
    /// <param name="properties">Structured properties attached to the generated NLog events.</param>
    public void Trace(string message, Exception exception, params LogProperty[] properties) => WriteEvent(LogLevel.Trace, message, exception, properties);

    /// <summary>Writes a Debug-level one-shot event with an attached exception.</summary>
    /// <param name="message">Message text rendered before the exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event and also rendered through MooreLib continuation formatting.</param>
    /// <param name="properties">Structured properties attached to the generated NLog events.</param>
    public void Debug(string message, Exception exception, params LogProperty[] properties) => WriteEvent(LogLevel.Debug, message, exception, properties);

    /// <summary>Writes a Info-level one-shot event with an attached exception.</summary>
    /// <param name="message">Message text rendered before the exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event and also rendered through MooreLib continuation formatting.</param>
    /// <param name="properties">Structured properties attached to the generated NLog events.</param>
    public void Info(string message, Exception exception, params LogProperty[] properties) => WriteEvent(LogLevel.Info, message, exception, properties);

    /// <summary>Writes a Warning-level one-shot event with an attached exception.</summary>
    /// <param name="message">Message text rendered before the exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event and also rendered through MooreLib continuation formatting.</param>
    /// <param name="properties">Structured properties attached to the generated NLog events.</param>
    public void Warn(string message, Exception exception, params LogProperty[] properties) => WriteEvent(LogLevel.Warning, message, exception, properties);

    /// <summary>Writes a Error-level one-shot event with an attached exception.</summary>
    /// <param name="message">Message text rendered before the exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event and also rendered through MooreLib continuation formatting.</param>
    /// <param name="properties">Structured properties attached to the generated NLog events.</param>
    public void Error(string message, Exception exception, params LogProperty[] properties) => WriteEvent(LogLevel.Error, message, exception, properties);

    /// <summary>Writes a Fatal-level one-shot event with an attached exception.</summary>
    /// <param name="message">Message text rendered before the exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event and also rendered through MooreLib continuation formatting.</param>
    /// <param name="properties">Structured properties attached to the generated NLog events.</param>
    public void Fatal(string message, Exception exception, params LogProperty[] properties) => WriteEvent(LogLevel.Fatal, message, exception, properties);

    /// <summary>Writes a Trace-level one-shot nested child beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Child message text.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void Trace(LogEntry parentEntry, string message, params LogProperty[] properties) => WriteEvent(parentEntry, LogLevel.Trace, message, properties);

    /// <summary>Writes a Debug-level one-shot nested child beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Child message text.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void Debug(LogEntry parentEntry, string message, params LogProperty[] properties) => WriteEvent(parentEntry, LogLevel.Debug, message, properties);

    /// <summary>Writes a Info-level one-shot nested child beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Child message text.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void Info(LogEntry parentEntry, string message, params LogProperty[] properties) => WriteEvent(parentEntry, LogLevel.Info, message, properties);

    /// <summary>Writes a Warning-level one-shot nested child beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Child message text.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void Warn(LogEntry parentEntry, string message, params LogProperty[] properties) => WriteEvent(parentEntry, LogLevel.Warning, message, properties);

    /// <summary>Writes a Error-level one-shot nested child beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Child message text.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void Error(LogEntry parentEntry, string message, params LogProperty[] properties) => WriteEvent(parentEntry, LogLevel.Error, message, properties);

    /// <summary>Writes a Fatal-level one-shot nested child beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Child message text.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void Fatal(LogEntry parentEntry, string message, params LogProperty[] properties) => WriteEvent(parentEntry, LogLevel.Fatal, message, properties);

    /// <summary>Writes a Trace-level one-shot nested child with an attached exception.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Child message text rendered before the exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event and also rendered through MooreLib continuation formatting.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void Trace(LogEntry parentEntry, string message, Exception exception, params LogProperty[] properties) => WriteEvent(parentEntry, LogLevel.Trace, message, exception, properties);

    /// <summary>Writes a Debug-level one-shot nested child with an attached exception.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Child message text rendered before the exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event and also rendered through MooreLib continuation formatting.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void Debug(LogEntry parentEntry, string message, Exception exception, params LogProperty[] properties) => WriteEvent(parentEntry, LogLevel.Debug, message, exception, properties);

    /// <summary>Writes a Info-level one-shot nested child with an attached exception.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Child message text rendered before the exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event and also rendered through MooreLib continuation formatting.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void Info(LogEntry parentEntry, string message, Exception exception, params LogProperty[] properties) => WriteEvent(parentEntry, LogLevel.Info, message, exception, properties);

    /// <summary>Writes a Warning-level one-shot nested child with an attached exception.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Child message text rendered before the exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event and also rendered through MooreLib continuation formatting.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void Warn(LogEntry parentEntry, string message, Exception exception, params LogProperty[] properties) => WriteEvent(parentEntry, LogLevel.Warning, message, exception, properties);

    /// <summary>Writes a Error-level one-shot nested child with an attached exception.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Child message text rendered before the exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event and also rendered through MooreLib continuation formatting.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void Error(LogEntry parentEntry, string message, Exception exception, params LogProperty[] properties) => WriteEvent(parentEntry, LogLevel.Error, message, exception, properties);

    /// <summary>Writes a Fatal-level one-shot nested child with an attached exception.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Child message text rendered before the exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event and also rendered through MooreLib continuation formatting.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void Fatal(LogEntry parentEntry, string message, Exception exception, params LogProperty[] properties) => WriteEvent(parentEntry, LogLevel.Fatal, message, exception, properties);

    /// <summary>Writes a one-shot event at the supplied level.</summary>
    /// <param name="level">Event severity.</param>
    /// <param name="message">Message text.</param>
    /// <param name="properties">Structured properties attached to the event.</param>
    public void WriteEvent(LogLevel level, string message, params LogProperty[] properties) =>
        WriteEventCore(level, message, exception: null, properties);

    /// <summary>Writes a one-shot event with an attached exception at the supplied level.</summary>
    /// <param name="level">Event severity.</param>
    /// <param name="message">Message text rendered before exception continuation lines.</param>
    /// <param name="exception">Exception retained on the first NLog event.</param>
    /// <param name="properties">Structured properties attached to the event.</param>
    public void WriteEvent(LogLevel level, string message, Exception exception, params LogProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(exception);
        WriteEventCore(level, message, exception, properties);
    }

    /// <summary>Writes a one-shot nested child beneath the supplied active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="level">Child event severity.</param>
    /// <param name="message">Child message text.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void WriteEvent(LogEntry parentEntry, LogLevel level, string message, params LogProperty[] properties) =>
        WriteAttachedEventCore(parentEntry, level, message, exception: null, properties, completeParent: false);

    /// <summary>Writes a one-shot nested child with an attached exception beneath the supplied active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="level">Child event severity.</param>
    /// <param name="message">Child message text.</param>
    /// <param name="exception">Exception retained on the first NLog event.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void WriteEvent(LogEntry parentEntry, LogLevel level, string message, Exception exception, params LogProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(exception);
        WriteAttachedEventCore(parentEntry, level, message, exception, properties, completeParent: false);
    }

    /// <summary>Writes exactly one unformatted blank physical line to every enabled destination.</summary>
    /// <remarks>
    /// This is a physical-stream command and bypasses normal severity filtering. If another entry owns
    /// an open inline line, that line is interrupted before the blank line is emitted. If no destination
    /// is enabled, the operation has no physical-stream effect.
    /// </remarks>
    public void WriteBlankLine()
    {
        lock (_coordinatorSync)
        {
            ThrowIfNotActiveLocked();
            if (!IsVisibleAtAnyDestinationLocked(LogLevel.Trace, bypassSeverityFiltering: true))
            {
                return;
            }

            // Use a dispatch severity high enough to pass every enabled NLog target rule. The blank
            // physical layout renders no semantic level/header, so this level is routing-only.
            var dispatchLevel = GetPhysicalCommandDispatchLevelLocked();
            var logEvent = CreatePhysicalEvent(dispatchLevel, string.Empty, null, PhysicalOutputKind.BlankLine);
            EmitPhysicalLocked(logEvent, null, dispatchLevel, bypassSeverityFiltering: true);
        }
    }
}
