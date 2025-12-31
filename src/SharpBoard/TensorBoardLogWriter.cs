using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace SharpBoard;

/// <summary>Creates TensorBoard-compatible event files and logs scalar values.</summary>
/// <remarks>
/// Thread safety: instances are not thread-safe; synchronize access if used from multiple threads.
/// Scalars are written as <c>TensorProto</c> with <c>dtype=DT_DOUBLE</c> to preserve full precision.
/// </remarks>
public sealed class TensorBoardLogWriter: IDisposable {
    /// <summary>The default TensorBoard file version string written as the first event.</summary>
    public const string DEFAULT_FILE_VERSION = "brain.Event:2";

    /// <summary>The default TensorBoard file version string written as the first event.</summary>
    public static string DefaultFileVersion => DEFAULT_FILE_VERSION;

    private readonly TensorBoardEventFileWriter eventWriter;
    private bool disposed;

    /// <summary>Creates a new event file in the given directory.</summary>
    /// <param name="logDirectory">Directory where the event file is created.</param>
    /// <param name="fileName">Optional event file name; if omitted, a TensorBoard-style name is generated.</param>
    public TensorBoardLogWriter(string logDirectory, string? fileName = null) {
        if (logDirectory is null) throw new ArgumentNullException(nameof(logDirectory));
        Directory.CreateDirectory(logDirectory);

        fileName ??= CreateDefaultEventFileName();
        this.FilePath = Path.Combine(logDirectory, fileName);

        this.eventWriter = TensorBoardEventFileWriter.Create(this.FilePath);
        this.eventWriter.Write(TensorBoardEvent.CreateFileVersion(DEFAULT_FILE_VERSION));
        this.eventWriter.Flush();
    }

    /// <summary>Full path to the created event file.</summary>
    public string FilePath { get; }

    /// <summary>Appends a scalar summary value.</summary>
    /// <remarks>The value is serialized as a scalar <c>TensorProto</c> with <c>dtype=DT_DOUBLE</c>.</remarks>
    public void AddScalar(string tag, double value, long step, DateTimeOffset? wallTime = null) {
        if (tag is null) throw new ArgumentNullException(nameof(tag));
        this.ThrowIfDisposed();
        this.eventWriter.Write(TensorBoardEvent.CreateScalar(tag, value, step, wallTime));
    }

    /// <summary>Flushes the underlying event file stream.</summary>
    public void Flush() {
        this.ThrowIfDisposed();
        this.eventWriter.Flush();
    }

    /// <inheritdoc />
    public void Dispose() {
        if (this.disposed) return;
        this.disposed = true;
        this.eventWriter.Dispose();
    }

    private static string CreateDefaultEventFileName() {
        long unixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string host = Environment.MachineName;
        int pid = Process.GetCurrentProcess().Id;
        string suffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        return $"events.out.tfevents.{unixSeconds}.{host}.{pid}.{suffix}";
    }

    private void ThrowIfDisposed() {
        if (this.disposed) throw new ObjectDisposedException(this.GetType().FullName);
    }
}