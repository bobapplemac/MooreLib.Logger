// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NLog.Layouts;
using NLog.LayoutRenderers;
using NLog.Targets;
using NLogEventInfo = NLog.LogEventInfo;

namespace MooreLib.Logging
{
    public sealed partial class Logger
    {
        private static string FormatRootBegin(string message) => message;

        private string FormatEntryBeginPrefix(LogEntry entry, bool terminal, string message) =>
            string.Concat(
                CreateAncestryColumns(entry, includeImmediateParent: false),
                terminal ? "└" : "├",
                message.Length == 0 ? string.Empty : " ");

        private string FormatContinuationLine(LogEntry entry, string message) =>
            FormatEntryContentLine(entry, terminal: false, message);

        private string FormatEndLine(LogEntry entry, string message) =>
            FormatEntryContentLine(entry, terminal: true, message);

        private string FormatTreeClosureLine(LogEntry entry) =>
            string.Concat(CreateAncestryColumns(entry, includeImmediateParent: true), "┴");

        private string FormatTerminalChildContentLine(LogEntry entry, bool terminal, string message) =>
            message.Length == 0
                ? string.Concat(CreateAncestryColumns(entry, includeImmediateParent: true, forceCompletedAncestor: entry.Parent), terminal ? "└" : "├")
                : string.Concat(CreateAncestryColumns(entry, includeImmediateParent: true, forceCompletedAncestor: entry.Parent), terminal ? "└ " : "├ ", message);

        private string FormatEntryContentLine(LogEntry entry, bool terminal, string message) =>
            message.Length == 0
                ? string.Concat(CreateAncestryColumns(entry, includeImmediateParent: true), terminal ? "└" : "├")
                : string.Concat(CreateAncestryColumns(entry, includeImmediateParent: true), terminal ? "└ " : "├ ", message);

        private string FormatInlineResume(LogEntry entry, string message)
        {
            var prefix = message.Length == 0
                ? _options.InlineResumePrefix.TrimEnd()
                : _options.InlineResumePrefix;
            return string.Concat(CreateAncestryColumns(entry, includeImmediateParent: true), prefix, message);
        }

        private string FormatStandaloneBranchPrefix(bool terminal, string message) =>
            string.Concat(terminal ? "└" : "├", message.Length == 0 ? string.Empty : " ");

        private string CreateAncestryColumns(
            LogEntry entry,
            bool includeImmediateParent,
            LogEntry forceCompletedAncestor = null)
        {
            if (_options.EntryIndentSize == 0) return string.Empty;

            var ancestors = new List<LogEntry>();
            for (var current = entry.Parent; current != null; current = current.Parent)
            {
                ancestors.Add(current);
            }

            ancestors.Reverse();
            var count = includeImmediateParent ? ancestors.Count : Math.Max(0, ancestors.Count - 1);
            if (count == 0) return string.Empty;

            var builder = new StringBuilder(checked(count * _options.EntryIndentSize));
            for (var i = 0; i < count; i++)
            {
                var ancestor = ancestors[i];
                if (ancestor.IsActive && !ReferenceEquals(ancestor, forceCompletedAncestor))
                {
                    builder.Append('│');
                    if (_options.EntryIndentSize > 1)
                    {
                        builder.Append(' ', _options.EntryIndentSize - 1);
                    }
                }
                else
                {
                    builder.Append(' ', _options.EntryIndentSize);
                }
            }

            return builder.ToString();
        }

        private static string NormalizeSingleLineMessage(string value)
        {
            return value
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        /// <summary>
        /// Splits text into physical lines while preserving every explicit line boundary, including
        /// empty trailing lines. CR, LF, and CRLF are each treated as one physical line separator.
        /// </summary>
        /// <remarks>
        /// Examples: <c>""</c> becomes one empty line; <c>"a\n"</c> becomes <c>["a", ""]</c>;
        /// <c>"\n\n"</c> becomes three empty lines.
        /// </remarks>
        internal static string[] SplitPhysicalLines(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var lines = new List<string>();
            var start = 0;

            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == '\r')
                {
                    lines.Add(value.Substring(start, i - start));

                    if (i + 1 < value.Length && value[i + 1] == '\n')
                    {
                        i++;
                    }

                    start = i + 1;
                }
                else if (value[i] == '\n')
                {
                    lines.Add(value.Substring(start, i - start));
                    start = i + 1;
                }
            }

            lines.Add(value.Substring(start));
            return lines.ToArray();
        }

        private sealed class StandardLineLayout : Layout
        {
            private readonly bool _includeTimestamp;
            private readonly bool _includeLogLevel;
            private readonly bool _includeEventProperties;
            private readonly string _messageSeparator;

            public StandardLineLayout(
                bool includeTimestamp,
                bool includeLogLevel,
                bool includeEventProperties,
                bool includeEntryMetadata,
                string timestampFormat,
                LogTimestampZone timestampZone,
                string messageSeparator)
            {
                _includeTimestamp = includeTimestamp;
                _includeLogLevel = includeLogLevel;
                _includeEventProperties = includeEventProperties;
                _messageSeparator = messageSeparator;

                TimestampRenderer = new DateLayoutRenderer
                {
                    Format = timestampFormat,
                    UniversalTime = timestampZone == LogTimestampZone.Utc,
                    Culture = CultureInfo.InvariantCulture
                };

                LevelRenderer = new LevelLayoutRenderer { Uppercase = true };
                EventPropertiesRenderer = new AllEventPropertiesLayoutRenderer
                {
                    Format = "[[key]=[value]]",
                    Separator = string.Empty,
                    Culture = CultureInfo.InvariantCulture
                };

                if (!includeEntryMetadata)
                {
                    EventPropertiesRenderer.Exclude = new HashSet<string>(StringComparer.Ordinal)
                {
                    InstanceIdPropertyName,
                    EntrySequencePropertyName,
                    ParentEntrySequencePropertyName,
                    EntryTypePropertyName,
                    EntryDepthPropertyName
                };
                }

                MessageLayout = Layout.FromString("${message}");
            }

            public DateLayoutRenderer TimestampRenderer { get; set; }
            public LevelLayoutRenderer LevelRenderer { get; set; }
            public AllEventPropertiesLayoutRenderer EventPropertiesRenderer { get; set; }
            public Layout MessageLayout { get; set; }

            protected override string GetFormattedMessage(NLogEventInfo logEvent)
            {
                var builder = new StringBuilder();
                if (_includeTimestamp) AppendPrefixPart(builder, TimestampRenderer.Render(logEvent));
                if (_includeLogLevel) AppendPrefixPart(builder, $"[{LevelRenderer.Render(logEvent)}]");
                if (_includeEventProperties) AppendPrefixPart(builder, EventPropertiesRenderer.Render(logEvent));

                var renderedMessage = MessageLayout.Render(logEvent);
                if (renderedMessage.Length > 0)
                {
                    if (builder.Length > 0) builder.Append(_messageSeparator);
                    builder.Append(renderedMessage);
                }

                return builder.ToString();
            }

            private static void AppendPrefixPart(StringBuilder builder, string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                if (builder.Length > 0) builder.Append(' ');
                builder.Append(value);
            }
        }

        /*
        private sealed class PhysicalOutputLayout : Layout
        {
            public PhysicalOutputLayout(Layout lineLayout, string fragmentLayout)
            {
                ArgumentNullException.ThrowIfNull(lineLayout);
                ArgumentException.ThrowIfNullOrWhiteSpace(fragmentLayout);
                LineLayout = lineLayout;
                FragmentLayout = Layout.FromString(fragmentLayout);
            }

            public Layout LineLayout { get; set; }
            public Layout FragmentLayout { get; set; }

            protected override string GetFormattedMessage(NLogEventInfo logEvent)
            {
                var kind = logEvent is PhysicalEvent physicalEvent
                    ? physicalEvent.OutputKind
                    : PhysicalOutputKind.NormalLine;
                var prefix = logEvent is PhysicalEvent prefixed ? prefixed.PhysicalPrefix : string.Empty;

                return kind switch
                {
                    PhysicalOutputKind.BlankLine or PhysicalOutputKind.ForcedLineBreak => Environment.NewLine,
                    PhysicalOutputKind.HeaderLine => string.Concat(prefix, LineLayout.Render(logEvent), Environment.NewLine),
                    PhysicalOutputKind.HeaderLineOpen => string.Concat(prefix, LineLayout.Render(logEvent)),
                    PhysicalOutputKind.Fragment => FragmentLayout.Render(logEvent),
                    PhysicalOutputKind.FragmentLine or PhysicalOutputKind.FragmentLineEnd =>
                        string.Concat(FragmentLayout.Render(logEvent), Environment.NewLine),
                    PhysicalOutputKind.PrefixedFragmentLine =>
                        string.Concat(prefix, FragmentLayout.Render(logEvent), Environment.NewLine),
                    PhysicalOutputKind.FragmentLineOpen => string.Concat(prefix, FragmentLayout.Render(logEvent)),
                    _ => string.Concat(prefix, LineLayout.Render(logEvent), Environment.NewLine)
                };
            }
        }
        */

        private sealed class PhysicalOutputLayout : Layout
        {
            public PhysicalOutputLayout(Layout lineLayout, string fragmentLayout)
            {
                if (lineLayout == null)
                    throw new ArgumentNullException(nameof(lineLayout));

                if (string.IsNullOrWhiteSpace(fragmentLayout))
                    throw new ArgumentException("Fragment layout cannot be null or whitespace.", nameof(fragmentLayout));

                LineLayout = lineLayout;
                FragmentLayout = Layout.FromString(fragmentLayout);
            }

            public Layout LineLayout { get; set; }
            public Layout FragmentLayout { get; set; }

            protected override string GetFormattedMessage(NLogEventInfo logEvent)
            {
                var physicalEvent = logEvent as PhysicalEvent;

                var kind = physicalEvent != null
                    ? physicalEvent.OutputKind
                    : PhysicalOutputKind.NormalLine;

                var prefix = physicalEvent != null
                    ? physicalEvent.PhysicalPrefix
                    : string.Empty;

                switch (kind)
                {
                    case PhysicalOutputKind.BlankLine:
                    case PhysicalOutputKind.ForcedLineBreak:
                        return Environment.NewLine;

                    case PhysicalOutputKind.HeaderLine:
                        return string.Concat(
                            prefix,
                            LineLayout.Render(logEvent),
                            Environment.NewLine);

                    case PhysicalOutputKind.HeaderLineOpen:
                        return string.Concat(prefix, LineLayout.Render(logEvent));

                    case PhysicalOutputKind.Fragment:
                        return FragmentLayout.Render(logEvent);

                    case PhysicalOutputKind.FragmentLine:
                    case PhysicalOutputKind.FragmentLineEnd:
                        return string.Concat(FragmentLayout.Render(logEvent), Environment.NewLine);

                    case PhysicalOutputKind.PrefixedFragmentLine:
                        return string.Concat(prefix, FragmentLayout.Render(logEvent), Environment.NewLine);

                    case PhysicalOutputKind.FragmentLineOpen:
                        return string.Concat(prefix, FragmentLayout.Render(logEvent));

                    default:
                        return string.Concat(prefix, LineLayout.Render(logEvent), Environment.NewLine);
                }
            }
        }

        private sealed class ExactConsoleTarget : TargetWithLayout
        {
            private readonly bool _useStandardError;

            public ExactConsoleTarget(string name, bool useStandardError)
            {
                Name = name;
                _useStandardError = useStandardError;
            }

            public bool AutoFlush { get; set; } = true;

            protected override void Write(NLogEventInfo logEvent)
            {
                var writer = _useStandardError ? Console.Error : Console.Out;
                writer.Write(RenderLogEvent(Layout, logEvent));
                if (AutoFlush) writer.Flush();
            }
        }
    }
}