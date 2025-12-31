using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using SharpBoard.Internal;

namespace SharpBoard;

/// <summary>Reads TensorBoard event files (TFRecord stream of <c>Event</c> protobuf messages).</summary>
public sealed class TensorBoardEventFileReader: IDisposable {
    private readonly Stream stream;
    private readonly bool leaveOpen;
    private readonly bool validateChecksums;
    private bool disposed;

    /// <summary>Creates a reader for a TFRecord stream.</summary>
    public TensorBoardEventFileReader(Stream inputStream, bool validateChecksums = true, bool leaveOpen = false) {
        this.stream = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        this.validateChecksums = validateChecksums;
        this.leaveOpen = leaveOpen;
    }

    /// <summary>Opens a TensorBoard event file for reading.</summary>
    public static TensorBoardEventFileReader Open(string path, bool validateChecksums = true) {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return new TensorBoardEventFileReader(stream, validateChecksums: validateChecksums, leaveOpen: false);
    }

    /// <summary>Reads the next TFRecord from the stream.</summary>
    public bool TryRead([NotNullWhen(true)] out TensorBoardEventRecord? record) {
        this.ThrowIfDisposed();

        if (!TfRecord.TryReadRecord(this.stream, this.validateChecksums, out byte[] data)) {
            record = null;
            return false;
        }

        record = new TensorBoardEventRecord(data);
        return true;
    }

    /// <inheritdoc />
    public void Dispose() {
        if (this.disposed) return;
        this.disposed = true;
        if (!this.leaveOpen) this.stream.Dispose();
    }

    private void ThrowIfDisposed() {
        if (this.disposed) throw new ObjectDisposedException(this.GetType().FullName);
    }
}
