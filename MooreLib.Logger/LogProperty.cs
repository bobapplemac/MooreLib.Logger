// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

namespace MooreLib.Logging
{
    /// <summary>Represents a structured property attached to an NLog event.</summary>
    public readonly struct LogProperty
    {
        /// <summary>Gets the property name exposed to NLog targets and layouts.</summary>
        public string Name { get; }

        /// <summary>Gets the property value. A value may be <see langword="null"/>.</summary>
        public object Value { get; }

        /// <summary>Initializes a new structured log property.</summary>
        /// <param name="name">The property name exposed to NLog targets and layouts.</param>
        /// <param name="value">The property value. A value may be <see langword="null"/>.</param>
        public LogProperty(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }
}