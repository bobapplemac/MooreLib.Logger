// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

namespace MooreLib.Logging;

public sealed partial class Logger
{
    /// <summary>Begins a logical entry and terminates its initial physical line.</summary>
    /// <param name="level">Entry severity.</param>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties retained by the entry and inherited by children.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginEntry(LogLevel level, string message, params LogProperty[] properties) =>
        BeginEntryCore(level, message, leaveLineOpen: false, explicitParent: null, useExplicitParent: false, exception: null, properties);

    /// <summary>Begins a logical entry with an attached exception and terminates its initial physical line.</summary>
    /// <param name="level">Entry severity.</param>
    /// <param name="message">Initial entry message.</param>
    /// <param name="exception">Exception retained on the first NLog event and rendered as continuation content.</param>
    /// <param name="properties">Structured properties retained by the entry and inherited by children.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginEntry(LogLevel level, string message, Exception exception, params LogProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return BeginEntryCore(level, message, leaveLineOpen: false, explicitParent: null, useExplicitParent: false, exception, properties);
    }

    /// <summary>Begins a logical entry and leaves its initial physical line open.</summary>
    /// <param name="level">Entry severity.</param>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties retained by the entry and inherited by children.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInline(LogLevel level, string message, params LogProperty[] properties) =>
        BeginEntryCore(level, message, leaveLineOpen: true, explicitParent: null, useExplicitParent: false, exception: null, properties);

    /// <summary>Begins a logical child entry beneath an active parent and terminates its initial physical line.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="level">Child entry severity.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginEntry(LogEntry parentEntry, LogLevel level, string message, params LogProperty[] properties) =>
        BeginEntryCore(level, message, leaveLineOpen: false, parentEntry, useExplicitParent: true, exception: null, properties);

    /// <summary>Begins a logical child entry with an attached exception beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="level">Child entry severity.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="exception">Exception retained on the first NLog event and rendered as continuation content.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginEntry(LogEntry parentEntry, LogLevel level, string message, Exception exception, params LogProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return BeginEntryCore(level, message, leaveLineOpen: false, parentEntry, useExplicitParent: true, exception, properties);
    }

    /// <summary>Begins an inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="level">Child entry severity.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInline(LogEntry parentEntry, LogLevel level, string message, params LogProperty[] properties) =>
        BeginEntryCore(level, message, leaveLineOpen: true, parentEntry, useExplicitParent: true, exception: null, properties);

    /// <summary>Begins a Trace-level logical entry and closes its initial physical line.</summary>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginTrace(string message, params LogProperty[] properties) => BeginEntry(LogLevel.Trace, message, properties);
    /// <summary>Begins a Debug-level logical entry and closes its initial physical line.</summary>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginDebug(string message, params LogProperty[] properties) => BeginEntry(LogLevel.Debug, message, properties);
    /// <summary>Begins a Info-level logical entry and closes its initial physical line.</summary>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInfo(string message, params LogProperty[] properties) => BeginEntry(LogLevel.Info, message, properties);
    /// <summary>Begins a Warning-level logical entry and closes its initial physical line.</summary>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginWarn(string message, params LogProperty[] properties) => BeginEntry(LogLevel.Warning, message, properties);
    /// <summary>Begins a Error-level logical entry and closes its initial physical line.</summary>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginError(string message, params LogProperty[] properties) => BeginEntry(LogLevel.Error, message, properties);
    /// <summary>Begins a Fatal-level logical entry and closes its initial physical line.</summary>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginFatal(string message, params LogProperty[] properties) => BeginEntry(LogLevel.Fatal, message, properties);

    /// <summary>Begins a Trace-level logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginTrace(LogEntry parentEntry, string message, params LogProperty[] properties) => BeginEntry(parentEntry, LogLevel.Trace, message, properties);
    /// <summary>Begins a Debug-level logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginDebug(LogEntry parentEntry, string message, params LogProperty[] properties) => BeginEntry(parentEntry, LogLevel.Debug, message, properties);
    /// <summary>Begins a Info-level logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInfo(LogEntry parentEntry, string message, params LogProperty[] properties) => BeginEntry(parentEntry, LogLevel.Info, message, properties);
    /// <summary>Begins a Warning-level logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginWarn(LogEntry parentEntry, string message, params LogProperty[] properties) => BeginEntry(parentEntry, LogLevel.Warning, message, properties);
    /// <summary>Begins a Error-level logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginError(LogEntry parentEntry, string message, params LogProperty[] properties) => BeginEntry(parentEntry, LogLevel.Error, message, properties);
    /// <summary>Begins a Fatal-level logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginFatal(LogEntry parentEntry, string message, params LogProperty[] properties) => BeginEntry(parentEntry, LogLevel.Fatal, message, properties);

    /// <summary>Begins a Trace-level logical entry and leaves its initial physical line open.</summary>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInlineTrace(string message, params LogProperty[] properties) => BeginInline(LogLevel.Trace, message, properties);
    /// <summary>Begins a Debug-level logical entry and leaves its initial physical line open.</summary>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInlineDebug(string message, params LogProperty[] properties) => BeginInline(LogLevel.Debug, message, properties);
    /// <summary>Begins a Info-level logical entry and leaves its initial physical line open.</summary>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInlineInfo(string message, params LogProperty[] properties) => BeginInline(LogLevel.Info, message, properties);
    /// <summary>Begins a Warning-level logical entry and leaves its initial physical line open.</summary>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInlineWarn(string message, params LogProperty[] properties) => BeginInline(LogLevel.Warning, message, properties);
    /// <summary>Begins a Error-level logical entry and leaves its initial physical line open.</summary>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInlineError(string message, params LogProperty[] properties) => BeginInline(LogLevel.Error, message, properties);
    /// <summary>Begins a Fatal-level logical entry and leaves its initial physical line open.</summary>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInlineFatal(string message, params LogProperty[] properties) => BeginInline(LogLevel.Fatal, message, properties);

    /// <summary>Begins a Trace-level inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInlineTrace(LogEntry parentEntry, string message, params LogProperty[] properties) => BeginInline(parentEntry, LogLevel.Trace, message, properties);
    /// <summary>Begins a Debug-level inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInlineDebug(LogEntry parentEntry, string message, params LogProperty[] properties) => BeginInline(parentEntry, LogLevel.Debug, message, properties);
    /// <summary>Begins a Info-level inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInlineInfo(LogEntry parentEntry, string message, params LogProperty[] properties) => BeginInline(parentEntry, LogLevel.Info, message, properties);
    /// <summary>Begins a Warning-level inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInlineWarn(LogEntry parentEntry, string message, params LogProperty[] properties) => BeginInline(parentEntry, LogLevel.Warning, message, properties);
    /// <summary>Begins a Error-level inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInlineError(LogEntry parentEntry, string message, params LogProperty[] properties) => BeginInline(parentEntry, LogLevel.Error, message, properties);
    /// <summary>Begins a Fatal-level inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle is used for explicit entry targeting.</returns>
    public LogEntry BeginInlineFatal(LogEntry parentEntry, string message, params LogProperty[] properties) => BeginInline(parentEntry, LogLevel.Fatal, message, properties);

    /// <summary>Writes text to the ambient logical entry without terminating its current physical line; without an ambient entry it falls back to an Info event.</summary>
    /// <param name="message">Message fragment to write.</param>
    /// <param name="properties">Structured properties attached to this physical event.</param>
    public void Write(string message, params LogProperty[] properties) =>
        WriteCore(explicitEntry: null, useExplicitEntry: false, message, endLine: false, properties);

    /// <summary>Writes text to an explicitly identified active logical entry without terminating its current physical line.</summary>
    /// <param name="entry">Active entry handle.</param>
    /// <param name="message">Message fragment to write.</param>
    /// <param name="properties">Structured properties attached to this physical event.</param>
    public void Write(LogEntry entry, string message, params LogProperty[] properties) =>
        WriteCore(entry, useExplicitEntry: true, message, endLine: false, properties);

    /// <summary>Writes text to the ambient logical entry and terminates the physical line while leaving the entry active.</summary>
    /// <param name="message">Message text to write before ending the physical line.</param>
    /// <param name="properties">Structured properties attached to the generated physical events.</param>
    public void WriteLine(string message, params LogProperty[] properties) =>
        WriteCore(explicitEntry: null, useExplicitEntry: false, message, endLine: true, properties);

    /// <summary>Writes text to an explicitly identified entry and terminates the physical line while leaving the entry active.</summary>
    /// <param name="entry">Active entry handle.</param>
    /// <param name="message">Message text to write before ending the physical line.</param>
    /// <param name="properties">Structured properties attached to the generated physical events.</param>
    public void WriteLine(LogEntry entry, string message, params LogProperty[] properties) =>
        WriteCore(entry, useExplicitEntry: true, message, endLine: true, properties);

    /// <summary>Terminates the current physical line of the ambient entry, or writes an empty standalone Info event if no entry is active.</summary>
    public void WriteLine() => WriteLine(string.Empty);

    /// <summary>Terminates the current physical line of an explicitly identified active entry.</summary>
    /// <param name="entry">Active entry handle.</param>
    public void WriteLine(LogEntry entry) => WriteLine(entry, string.Empty);

    /// <summary>Completes the ambient logical entry without adding message text.</summary>
    public void CompleteEntry() =>
        CompleteEntryCore(explicitEntry: null, useExplicitEntry: false, message: null, exception: null, properties: null);

    /// <summary>Completes the ambient logical entry and writes terminal message text.</summary>
    /// <param name="message">Terminal message text.</param>
    /// <param name="properties">Structured properties attached to the terminal output.</param>
    public void CompleteEntry(string message, params LogProperty[] properties) =>
        CompleteEntryCore(explicitEntry: null, useExplicitEntry: false, message, exception: null, properties);

    /// <summary>Completes the ambient logical entry with terminal message text and an attached exception.</summary>
    /// <param name="message">Terminal message text.</param>
    /// <param name="exception">Exception retained on the first generated NLog event.</param>
    /// <param name="properties">Structured properties attached to the terminal output.</param>
    public void CompleteEntry(string message, Exception exception, params LogProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(exception);
        CompleteEntryCore(explicitEntry: null, useExplicitEntry: false, message, exception, properties);
    }

    /// <summary>Completes an explicitly identified active logical entry without adding message text.</summary>
    /// <param name="entry">Active entry handle.</param>
    public void CompleteEntry(LogEntry entry) =>
        CompleteEntryCore(entry, useExplicitEntry: true, message: null, exception: null, properties: null);

    /// <summary>Completes an explicitly identified active logical entry and writes terminal message text.</summary>
    /// <param name="entry">Active entry handle.</param>
    /// <param name="message">Terminal message text.</param>
    /// <param name="properties">Structured properties attached to the terminal output.</param>
    public void CompleteEntry(LogEntry entry, string message, params LogProperty[] properties) =>
        CompleteEntryCore(entry, useExplicitEntry: true, message, exception: null, properties);

    /// <summary>Completes an explicitly identified active logical entry with terminal message text and an attached exception.</summary>
    /// <param name="entry">Active entry handle.</param>
    /// <param name="message">Terminal message text.</param>
    /// <param name="exception">Exception retained on the first generated NLog event.</param>
    /// <param name="properties">Structured properties attached to the terminal output.</param>
    public void CompleteEntry(LogEntry entry, string message, Exception exception, params LogProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(exception);
        CompleteEntryCore(entry, useExplicitEntry: true, message, exception, properties);
    }

    /// <summary>Creates a terminal one-shot child beneath an active parent and completes the parent in the same coordinated operation.</summary>
    /// <remarks>
    /// The child is known to be terminal before any physical output is emitted, so its opening line renders with a terminal
    /// branch marker and any additional multiline/exception detail is rendered beneath that child. No complete-tree buffering
    /// or future-sibling lookahead is required.
    /// </remarks>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="level">Severity assigned to the terminal child.</param>
    /// <param name="message">Terminal child message.</param>
    public void CompleteWithChild(LogEntry parentEntry, LogLevel level, string message) =>
        CompleteWithChild(parentEntry, level, message, Array.Empty<LogProperty>());

    /// <summary>Creates a terminal one-shot child beneath an active parent, attaches structured properties, and completes the parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="level">Severity assigned to the terminal child.</param>
    /// <param name="message">Terminal child message.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void CompleteWithChild(LogEntry parentEntry, LogLevel level, string message, params LogProperty[] properties) =>
        WriteAttachedEventCore(parentEntry, level, message, exception: null, properties, completeParent: true);

    /// <summary>Creates a terminal one-shot child with an attached exception beneath an active parent and completes the parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="level">Severity assigned to the terminal child.</param>
    /// <param name="message">Terminal child message.</param>
    /// <param name="exception">Exception retained on the first generated NLog event.</param>
    public void CompleteWithChild(LogEntry parentEntry, LogLevel level, string message, Exception exception) =>
        CompleteWithChild(parentEntry, level, message, exception, Array.Empty<LogProperty>());

    /// <summary>Creates a terminal one-shot child with an attached exception and structured properties beneath an active parent and completes the parent.</summary>
    /// <param name="parentEntry">Active parent entry handle.</param>
    /// <param name="level">Severity assigned to the terminal child.</param>
    /// <param name="message">Terminal child message.</param>
    /// <param name="exception">Exception retained on the first generated NLog event.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void CompleteWithChild(LogEntry parentEntry, LogLevel level, string message, Exception exception, params LogProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(exception);
        WriteAttachedEventCore(parentEntry, level, message, exception, properties, completeParent: true);
    }

    /// <summary>Begins the terminal physical line of the ambient entry and leaves it open for subsequent <see cref="Write(string, LogProperty[])"/> calls.</summary>
    /// <param name="message">Initial text for the terminal inline line.</param>
    /// <param name="properties">Structured properties attached to the terminal-line event.</param>
    public void CompleteEntryInline(string message, params LogProperty[] properties) =>
        CompleteEntryInlineCore(explicitEntry: null, useExplicitEntry: false, message, properties);

    /// <summary>Begins the terminal physical line of an explicitly identified entry and leaves it open for subsequent writes.</summary>
    /// <param name="entry">Active entry handle.</param>
    /// <param name="message">Initial text for the terminal inline line.</param>
    /// <param name="properties">Structured properties attached to the terminal-line event.</param>
    public void CompleteEntryInline(LogEntry entry, string message, params LogProperty[] properties) =>
        CompleteEntryInlineCore(entry, useExplicitEntry: true, message, properties);

}
