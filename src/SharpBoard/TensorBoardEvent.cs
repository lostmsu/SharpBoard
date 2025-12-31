using System;
using System.Collections.Generic;

namespace SharpBoard;

/// <summary>Represents a TensorBoard <c>Event</c> record.</summary>
public sealed class TensorBoardEvent {
    internal static readonly DateTimeOffset UnixEpoch = DateTimeOffset.FromUnixTimeMilliseconds(0);

    internal TensorBoardEvent(DateTimeOffset wallTime, long step, string? fileVersion, TensorBoardScalar[] scalars) {
        this.WallTime = wallTime;
        this.Step = step;
        this.FileVersion = fileVersion;
        this.Scalars = scalars ?? throw new ArgumentNullException(nameof(scalars));
    }

    /// <summary>Wall time of this event.</summary>
    /// <remarks>Serialized to TensorBoard as Unix timestamp seconds (double).</remarks>
    public DateTimeOffset WallTime { get; }

    internal double WallTimeSeconds => ToUnixTimeSecondsDouble(this.WallTime);
    /// <summary>Training step.</summary>
    public long Step { get; }
    /// <summary>TensorBoard file version string, if present.</summary>
    public string? FileVersion { get; }

    /// <summary>Scalar summaries in this event.</summary>
    public IReadOnlyList<TensorBoardScalar> Scalars { get; }

    /// <summary>Creates a file version event.</summary>
    public static TensorBoardEvent CreateFileVersion(string fileVersion, DateTimeOffset? wallTime = null) {
        if (fileVersion is null) throw new ArgumentNullException(nameof(fileVersion));
        return new TensorBoardEvent(
            wallTime: wallTime ?? DateTimeOffset.UtcNow,
            step: 0,
            fileVersion: fileVersion,
            scalars: []);
    }

    /// <summary>Creates an event with a single scalar summary.</summary>
    public static TensorBoardEvent CreateScalar(string tag, double value, long step, DateTimeOffset? wallTime = null) {
        if (tag is null) throw new ArgumentNullException(nameof(tag));
        return new TensorBoardEvent(
            wallTime: wallTime ?? DateTimeOffset.UtcNow,
            step: step,
            fileVersion: null,
            scalars: [new TensorBoardScalar(tag, value)]);
    }

    internal static DateTimeOffset FromUnixTimeSecondsDouble(double unixTimeSeconds) {
        return UnixEpoch + TimeSpan.FromSeconds(unixTimeSeconds);
    }

    internal static double ToUnixTimeSecondsDouble(DateTimeOffset wallTime) {
        return (wallTime.ToUniversalTime() - UnixEpoch).TotalSeconds;
    }
}
