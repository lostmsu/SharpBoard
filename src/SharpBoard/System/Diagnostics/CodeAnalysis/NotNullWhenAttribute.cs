#if NETSTANDARD2_0
using System;

// Polyfill for System.Diagnostics.CodeAnalysis.NotNullWhenAttribute for netstandard2.0.
// Roslyn recognizes this attribute by full name, regardless of assembly.
#pragma warning disable IDE0130
namespace System.Diagnostics.CodeAnalysis;
#pragma warning restore IDE0130

/// <summary>Specifies that an output will be non-null when the method returns the specified value.</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
internal sealed class NotNullWhenAttribute : Attribute {
    /// <summary>Initializes the attribute.</summary>
    /// <param name="returnValue">The return value condition that guarantees non-null.</param>
    public NotNullWhenAttribute(bool returnValue) {
        this.ReturnValue = returnValue;
    }

    /// <summary>The return value condition that guarantees non-null.</summary>
    public bool ReturnValue { get; }
}
#endif
