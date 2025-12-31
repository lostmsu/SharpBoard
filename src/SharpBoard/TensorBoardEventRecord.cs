using System;

using SharpBoard.Internal;

namespace SharpBoard;

/// <summary>A single TFRecord containing a serialized TensorBoard <c>Event</c> protobuf.</summary>
public sealed class TensorBoardEventRecord {
    private TensorBoardEvent? parsed;

    /// <summary>Creates a record from an <c>Event</c> protobuf payload.</summary>
    public TensorBoardEventRecord(byte[] eventProtoBytes) {
        this.ProtoBytes = eventProtoBytes ?? throw new ArgumentNullException(nameof(eventProtoBytes));
    }

    internal byte[] ProtoBytes { get; }

    /// <summary>Returns a copy of the serialized <c>Event</c> protobuf bytes.</summary>
    public byte[] GetEventProtoBytes() => (byte[])this.ProtoBytes.Clone();

    /// <summary>Parses the protobuf payload into a <see cref="TensorBoardEvent"/>.</summary>
    public TensorBoardEvent Parse() {
        this.parsed ??= TensorBoardProtos.ParseEvent(this.ProtoBytes);
        return this.parsed;
    }
}