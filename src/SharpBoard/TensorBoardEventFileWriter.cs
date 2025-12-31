using System;
using System.IO;

using SharpBoard.Internal;

namespace SharpBoard;

/// <summary>Writes TensorBoard event files (TFRecord stream of <c>Event</c> protobuf messages).</summary>
public sealed class TensorBoardEventFileWriter: IDisposable {
    private readonly Stream stream;
    private readonly bool leaveOpen;
    private bool disposed;

    /// <summary>Creates a writer for a TFRecord stream.</summary>
    public TensorBoardEventFileWriter(Stream outputStream, bool leaveOpen = false) {
        this.stream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
        this.leaveOpen = leaveOpen;
    }

    /// <summary>Creates a new TensorBoard event file for writing (overwriting if it exists).</summary>
    public static TensorBoardEventFileWriter Create(string path) {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        return new TensorBoardEventFileWriter(stream, leaveOpen: false);
    }

    /// <summary>Writes a record (raw <c>Event</c> protobuf bytes) into the TFRecord stream.</summary>
    public void Write(TensorBoardEventRecord record) {
        if (record is null) throw new ArgumentNullException(nameof(record));
        this.ThrowIfDisposed();
        byte[] proto = record.ProtoBytes;
        TfRecord.WriteRecord(this.stream, proto, 0, proto.Length);
    }

    /// <summary>Serializes and writes an event into the TFRecord stream.</summary>
    public void Write(TensorBoardEvent @event) {
        if (@event is null) throw new ArgumentNullException(nameof(@event));
        this.ThrowIfDisposed();
        byte[] proto = TensorBoardProtos.SerializeEvent(@event);
        TfRecord.WriteRecord(this.stream, proto, 0, proto.Length);
    }

    /// <summary>Flushes the underlying stream.</summary>
    public void Flush() {
        this.ThrowIfDisposed();
        this.stream.Flush();
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