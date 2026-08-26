// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

namespace MooreLib.Logging;

/// <summary>Represents a structured property attached to an NLog event.</summary>
/// <param name="Name">The property name exposed to NLog targets and layouts.</param>
/// <param name="Value">The property value. A value may be <see langword="null"/>.</param>
public readonly record struct LogProperty(string Name, object? Value);
