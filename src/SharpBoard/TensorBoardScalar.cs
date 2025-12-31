using System;

namespace SharpBoard;

/// <summary>Represents a scalar summary value (tag/value pair).</summary>
public readonly struct TensorBoardScalar: IEquatable<TensorBoardScalar> {
    /// <summary>Creates a scalar value.</summary>
    /// <param name="tag">TensorBoard tag.</param>
    /// <param name="value">Scalar value.</param>
    public TensorBoardScalar(string tag, double value) {
        this.Tag = tag ?? throw new ArgumentNullException(nameof(tag));
        this.Value = value;
    }

    /// <summary>TensorBoard tag.</summary>
    public string Tag { get; }
    /// <summary>Scalar value.</summary>
    public double Value { get; }

    /// <inheritdoc />
    public bool Equals(TensorBoardScalar other) => this.Tag == other.Tag && this.Value.Equals(other.Value);
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TensorBoardScalar other && this.Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() {
        unchecked {
            int hash = this.Tag.GetHashCode();
            hash = (hash * 397) ^ this.Value.GetHashCode();
            return hash;
        }
    }
    /// <summary>Compares two scalar values.</summary>
    public static bool operator ==(TensorBoardScalar left, TensorBoardScalar right) => left.Equals(right);
    /// <summary>Compares two scalar values.</summary>
    public static bool operator !=(TensorBoardScalar left, TensorBoardScalar right) => !left.Equals(right);
}