// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;

namespace MooreLib.Logging;

public sealed partial class Logger
{
    /// <summary>Begins a logical entry and terminates its initial physical line.</summary>
    /// <param name="level">Entry severity.</param>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties retained by the entry and inherited by children.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginEntry(LogLevel level, string message, params LogProperty[] properties) =>
        BeginEntryCore(level, message, leaveLineOpen: false, explicitParentId: null, exception: null, properties);

    /// <summary>Begins a logical entry with an attached exception and terminates its initial physical line.</summary>
    /// <param name="level">Entry severity.</param>
    /// <param name="message">Initial entry message.</param>
    /// <param name="exception">Exception retained on the first NLog event and rendered as continuation content.</param>
    /// <param name="properties">Structured properties retained by the entry and inherited by children.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginEntry(LogLevel level, string message, Exception exception, params LogProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return BeginEntryCore(level, message, leaveLineOpen: false, explicitParentId: null, exception, properties);
    }

    /// <summary>Begins a logical entry and leaves its initial physical line open.</summary>
    /// <param name="level">Entry severity.</param>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties retained by the entry and inherited by children.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInline(LogLevel level, string message, params LogProperty[] properties) =>
        BeginEntryCore(level, message, leaveLineOpen: true, explicitParentId: null, exception: null, properties);

    /// <summary>Begins a logical child entry beneath an active parent and terminates its initial physical line.</summary>
    /// <param name="parentEntryId">Identifier of the active parent entry.</param>
    /// <param name="level">Child entry severity.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginEntry(long parentEntryId, LogLevel level, string message, params LogProperty[] properties) =>
        BeginEntryCore(level, message, leaveLineOpen: false, parentEntryId, exception: null, properties);

    /// <summary>Begins a logical child entry with an attached exception beneath an active parent.</summary>
    /// <param name="parentEntryId">Identifier of the active parent entry.</param>
    /// <param name="level">Child entry severity.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="exception">Exception retained on the first NLog event and rendered as continuation content.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginEntry(long parentEntryId, LogLevel level, string message, Exception exception, params LogProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return BeginEntryCore(level, message, leaveLineOpen: false, parentEntryId, exception, properties);
    }

    /// <summary>Begins an inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Identifier of the active parent entry.</param>
    /// <param name="level">Child entry severity.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInline(long parentEntryId, LogLevel level, string message, params LogProperty[] properties) =>
        BeginEntryCore(level, message, leaveLineOpen: true, parentEntryId, exception: null, properties);

    /// <summary>Begins a Trace-level logical entry and closes its initial physical line.</summary>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginTrace(string message, params LogProperty[] properties) => BeginEntry(LogLevel.Trace, message, properties);
    /// <summary>Begins a Debug-level logical entry and closes its initial physical line.</summary>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginDebug(string message, params LogProperty[] properties) => BeginEntry(LogLevel.Debug, message, properties);
    /// <summary>Begins a Info-level logical entry and closes its initial physical line.</summary>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInfo(string message, params LogProperty[] properties) => BeginEntry(LogLevel.Info, message, properties);
    /// <summary>Begins a Warning-level logical entry and closes its initial physical line.</summary>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginWarn(string message, params LogProperty[] properties) => BeginEntry(LogLevel.Warning, message, properties);
    /// <summary>Begins a Error-level logical entry and closes its initial physical line.</summary>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginError(string message, params LogProperty[] properties) => BeginEntry(LogLevel.Error, message, properties);
    /// <summary>Begins a Fatal-level logical entry and closes its initial physical line.</summary>
    /// <param name="message">Initial entry message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginFatal(string message, params LogProperty[] properties) => BeginEntry(LogLevel.Fatal, message, properties);

    /// <summary>Begins a Trace-level logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Active parent entry identifier.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginTrace(long parentEntryId, string message, params LogProperty[] properties) => BeginEntry(parentEntryId, LogLevel.Trace, message, properties);
    /// <summary>Begins a Debug-level logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Active parent entry identifier.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginDebug(long parentEntryId, string message, params LogProperty[] properties) => BeginEntry(parentEntryId, LogLevel.Debug, message, properties);
    /// <summary>Begins a Info-level logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Active parent entry identifier.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInfo(long parentEntryId, string message, params LogProperty[] properties) => BeginEntry(parentEntryId, LogLevel.Info, message, properties);
    /// <summary>Begins a Warning-level logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Active parent entry identifier.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginWarn(long parentEntryId, string message, params LogProperty[] properties) => BeginEntry(parentEntryId, LogLevel.Warning, message, properties);
    /// <summary>Begins a Error-level logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Active parent entry identifier.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginError(long parentEntryId, string message, params LogProperty[] properties) => BeginEntry(parentEntryId, LogLevel.Error, message, properties);
    /// <summary>Begins a Fatal-level logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Active parent entry identifier.</param>
    /// <param name="message">Initial child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginFatal(long parentEntryId, string message, params LogProperty[] properties) => BeginEntry(parentEntryId, LogLevel.Fatal, message, properties);

    /// <summary>Begins a Trace-level logical entry and leaves its initial physical line open.</summary>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInlineTrace(string message, params LogProperty[] properties) => BeginInline(LogLevel.Trace, message, properties);
    /// <summary>Begins a Debug-level logical entry and leaves its initial physical line open.</summary>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInlineDebug(string message, params LogProperty[] properties) => BeginInline(LogLevel.Debug, message, properties);
    /// <summary>Begins a Info-level logical entry and leaves its initial physical line open.</summary>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInlineInfo(string message, params LogProperty[] properties) => BeginInline(LogLevel.Info, message, properties);
    /// <summary>Begins a Warning-level logical entry and leaves its initial physical line open.</summary>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInlineWarn(string message, params LogProperty[] properties) => BeginInline(LogLevel.Warning, message, properties);
    /// <summary>Begins a Error-level logical entry and leaves its initial physical line open.</summary>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInlineError(string message, params LogProperty[] properties) => BeginInline(LogLevel.Error, message, properties);
    /// <summary>Begins a Fatal-level logical entry and leaves its initial physical line open.</summary>
    /// <param name="message">Initial inline message.</param>
    /// <param name="properties">Structured properties inherited by later output and child entries.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInlineFatal(string message, params LogProperty[] properties) => BeginInline(LogLevel.Fatal, message, properties);

    /// <summary>Begins a Trace-level inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Active parent entry identifier.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInlineTrace(long parentEntryId, string message, params LogProperty[] properties) => BeginInline(parentEntryId, LogLevel.Trace, message, properties);
    /// <summary>Begins a Debug-level inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Active parent entry identifier.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInlineDebug(long parentEntryId, string message, params LogProperty[] properties) => BeginInline(parentEntryId, LogLevel.Debug, message, properties);
    /// <summary>Begins a Info-level inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Active parent entry identifier.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInlineInfo(long parentEntryId, string message, params LogProperty[] properties) => BeginInline(parentEntryId, LogLevel.Info, message, properties);
    /// <summary>Begins a Warning-level inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Active parent entry identifier.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInlineWarn(long parentEntryId, string message, params LogProperty[] properties) => BeginInline(parentEntryId, LogLevel.Warning, message, properties);
    /// <summary>Begins a Error-level inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Active parent entry identifier.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInlineError(long parentEntryId, string message, params LogProperty[] properties) => BeginInline(parentEntryId, LogLevel.Error, message, properties);
    /// <summary>Begins a Fatal-level inline logical child entry beneath an active parent.</summary>
    /// <param name="parentEntryId">Active parent entry identifier.</param>
    /// <param name="message">Initial inline child message.</param>
    /// <param name="properties">Structured properties to merge with inherited parent properties.</param>
    /// <returns>A disposable handle representing the newly created logical entry. The handle implicitly converts to its numeric entry ID.</returns>
    public LogEntry BeginInlineFatal(long parentEntryId, string message, params LogProperty[] properties) => BeginInline(parentEntryId, LogLevel.Fatal, message, properties);

    /// <summary>Writes text to the ambient logical entry without terminating its current physical line; without an ambient entry it falls back to an Info event.</summary>
    /// <param name="message">Message fragment to write.</param>
    /// <param name="properties">Structured properties attached to this physical event.</param>
    public void Write(string message, params LogProperty[] properties) =>
        WriteCore(entryId: null, explicitEntryId: false, message, endLine: false, properties);

    /// <summary>Writes text to an explicitly identified active logical entry without terminating its current physical line.</summary>
    /// <param name="entryId">Identifier of the active entry.</param>
    /// <param name="message">Message fragment to write.</param>
    /// <param name="properties">Structured properties attached to this physical event.</param>
    public void Write(long entryId, string message, params LogProperty[] properties) =>
        WriteCore(entryId, explicitEntryId: true, message, endLine: false, properties);

    /// <summary>Writes text to the ambient logical entry and terminates the physical line while leaving the entry active.</summary>
    /// <param name="message">Message text to write before ending the physical line.</param>
    /// <param name="properties">Structured properties attached to the generated physical events.</param>
    public void WriteLine(string message, params LogProperty[] properties) =>
        WriteCore(entryId: null, explicitEntryId: false, message, endLine: true, properties);

    /// <summary>Writes text to an explicitly identified entry and terminates the physical line while leaving the entry active.</summary>
    /// <param name="entryId">Identifier of the active entry.</param>
    /// <param name="message">Message text to write before ending the physical line.</param>
    /// <param name="properties">Structured properties attached to the generated physical events.</param>
    public void WriteLine(long entryId, string message, params LogProperty[] properties) =>
        WriteCore(entryId, explicitEntryId: true, message, endLine: true, properties);

    /// <summary>Terminates the current physical line of the ambient entry, or writes an empty standalone Info event if no entry is active.</summary>
    public void WriteLine() => WriteLine(string.Empty);

    /// <summary>Terminates the current physical line of an explicitly identified active entry.</summary>
    /// <param name="entryId">Identifier of the active entry.</param>
    public void WriteLine(long entryId) => WriteLine(entryId, string.Empty);

    /// <summary>Completes the ambient logical entry without adding message text.</summary>
    public void CompleteEntry() =>
        CompleteEntryCore(entryId: null, explicitEntryId: false, message: null, exception: null, properties: null);

    /// <summary>Completes the ambient logical entry and writes terminal message text.</summary>
    /// <param name="message">Terminal message text.</param>
    /// <param name="properties">Structured properties attached to the terminal output.</param>
    public void CompleteEntry(string message, params LogProperty[] properties) =>
        CompleteEntryCore(entryId: null, explicitEntryId: false, message, exception: null, properties);

    /// <summary>Completes the ambient logical entry with terminal message text and an attached exception.</summary>
    /// <param name="message">Terminal message text.</param>
    /// <param name="exception">Exception retained on the first generated NLog event.</param>
    /// <param name="properties">Structured properties attached to the terminal output.</param>
    public void CompleteEntry(string message, Exception exception, params LogProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(exception);
        CompleteEntryCore(entryId: null, explicitEntryId: false, message, exception, properties);
    }

    /// <summary>Completes an explicitly identified active logical entry without adding message text.</summary>
    /// <param name="entryId">Identifier of the active entry.</param>
    public void CompleteEntry(long entryId) =>
        CompleteEntryCore(entryId, explicitEntryId: true, message: null, exception: null, properties: null);

    /// <summary>Completes an explicitly identified active logical entry and writes terminal message text.</summary>
    /// <param name="entryId">Identifier of the active entry.</param>
    /// <param name="message">Terminal message text.</param>
    /// <param name="properties">Structured properties attached to the terminal output.</param>
    public void CompleteEntry(long entryId, string message, params LogProperty[] properties) =>
        CompleteEntryCore(entryId, explicitEntryId: true, message, exception: null, properties);

    /// <summary>Completes an explicitly identified active logical entry with terminal message text and an attached exception.</summary>
    /// <param name="entryId">Identifier of the active entry.</param>
    /// <param name="message">Terminal message text.</param>
    /// <param name="exception">Exception retained on the first generated NLog event.</param>
    /// <param name="properties">Structured properties attached to the terminal output.</param>
    public void CompleteEntry(long entryId, string message, Exception exception, params LogProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(exception);
        CompleteEntryCore(entryId, explicitEntryId: true, message, exception, properties);
    }

    /// <summary>Writes a terminal one-shot child at the supplied level and completes the active parent in the same operation.</summary>
    /// <param name="parentEntryId">Identifier of the active parent entry.</param>
    /// <param name="level">Severity assigned to the terminal child.</param>
    /// <param name="message">Terminal child message.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void CompleteEntry(long parentEntryId, LogLevel level, string message, params LogProperty[] properties) =>
        WriteAttachedEventCore(parentEntryId, level, message, exception: null, properties, completeParent: true);

    /// <summary>Writes a terminal one-shot child with an attached exception and completes the active parent in the same operation.</summary>
    /// <param name="parentEntryId">Identifier of the active parent entry.</param>
    /// <param name="level">Severity assigned to the terminal child.</param>
    /// <param name="message">Terminal child message.</param>
    /// <param name="exception">Exception retained on the first generated NLog event.</param>
    /// <param name="properties">Structured properties merged over inherited parent properties.</param>
    public void CompleteEntry(long parentEntryId, LogLevel level, string message, Exception exception, params LogProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(exception);
        WriteAttachedEventCore(parentEntryId, level, message, exception, properties, completeParent: true);
    }

    /// <summary>Begins the terminal physical line of the ambient entry and leaves it open for subsequent <see cref="Write(string, LogProperty[])"/> calls.</summary>
    /// <param name="message">Initial text for the terminal inline line.</param>
    /// <param name="properties">Structured properties attached to the terminal-line event.</param>
    public void CompleteEntryInline(string message, params LogProperty[] properties) =>
        CompleteEntryInlineCore(entryId: null, explicitEntryId: false, message, properties);

    /// <summary>Begins the terminal physical line of an explicitly identified entry and leaves it open for subsequent writes.</summary>
    /// <param name="entryId">Identifier of the active entry.</param>
    /// <param name="message">Initial text for the terminal inline line.</param>
    /// <param name="properties">Structured properties attached to the terminal-line event.</param>
    public void CompleteEntryInline(long entryId, string message, params LogProperty[] properties) =>
        CompleteEntryInlineCore(entryId, explicitEntryId: true, message, properties);
}
