using System;
using System.IO;
using System.Text;

namespace SharpBoard.Internal;

internal enum ProtoWireType: uint {
    Varint = 0,
    Fixed64 = 1,
    LengthDelimited = 2,
    StartGroup = 3,
    EndGroup = 4,
    Fixed32 = 5,
}

internal readonly struct ProtoBytes {
    public ProtoBytes(byte[] buffer, int offset, int length) {
        this.Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        if ((uint)offset > (uint)buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if ((uint)length > (uint)(buffer.Length - offset)) throw new ArgumentOutOfRangeException(nameof(length));
        this.Offset = offset;
        this.Length = length;
    }

    public byte[] Buffer { get; }
    public int Offset { get; }
    public int Length { get; }
}

internal ref struct ProtoReader {
    private readonly byte[] buffer;
    private readonly int end;
    private int pos;

    public ProtoReader(byte[] buffer) : this(buffer, 0, buffer?.Length ?? 0) { }

    public ProtoReader(byte[] buffer, int offset, int length) {
        this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        if ((uint)offset > (uint)buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if ((uint)length > (uint)(buffer.Length - offset)) throw new ArgumentOutOfRangeException(nameof(length));
        this.pos = offset;
        this.end = offset + length;
    }

    public bool TryReadTag(out uint fieldNumber, out ProtoWireType wireType) {
        if (this.pos >= this.end) {
            fieldNumber = 0;
            wireType = 0;
            return false;
        }

        uint tag = this.ReadVarint32();
        wireType = (ProtoWireType)(tag & 0x7u);
        fieldNumber = tag >> 3;

        if (fieldNumber == 0) throw new InvalidDataException("Invalid protobuf field number 0.");
        return true;
    }

    public uint ReadVarint32() {
        ulong value = this.ReadVarint64();
        if (value > uint.MaxValue) throw new InvalidDataException("Varint32 overflow.");
        return (uint)value;
    }

    public ulong ReadVarint64() {
        ulong result = 0;
        int shift = 0;
        while (shift < 64) {
            if (this.pos >= this.end) throw new EndOfStreamException();
            byte b = this.buffer[this.pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
        }

        throw new InvalidDataException("Varint64 overflow.");
    }

    public long ReadInt64() => unchecked((long)this.ReadVarint64());

    public uint ReadFixed32() {
        if (this.pos > this.end - 4) throw new EndOfStreamException();
        uint value =
            (uint)this.buffer[this.pos + 0] |
            ((uint)this.buffer[this.pos + 1] << 8) |
            ((uint)this.buffer[this.pos + 2] << 16) |
            ((uint)this.buffer[this.pos + 3] << 24);
        this.pos += 4;
        return value;
    }

    public ulong ReadFixed64() {
        if (this.pos > this.end - 8) throw new EndOfStreamException();
        ulong value =
            (ulong)this.buffer[this.pos + 0] |
            ((ulong)this.buffer[this.pos + 1] << 8) |
            ((ulong)this.buffer[this.pos + 2] << 16) |
            ((ulong)this.buffer[this.pos + 3] << 24) |
            ((ulong)this.buffer[this.pos + 4] << 32) |
            ((ulong)this.buffer[this.pos + 5] << 40) |
            ((ulong)this.buffer[this.pos + 6] << 48) |
            ((ulong)this.buffer[this.pos + 7] << 56);
        this.pos += 8;
        return value;
    }

    public double ReadDouble() {
        long bits = unchecked((long)this.ReadFixed64());
        return BitConverter.Int64BitsToDouble(bits);
    }

    public float ReadFloat() {
        uint bits = this.ReadFixed32();
        return BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)bits)), 0);
    }

    public ProtoBytes ReadBytes() {
        int length = checked((int)this.ReadVarint32());
        if (this.pos > this.end - length) throw new EndOfStreamException();
        var result = new ProtoBytes(this.buffer, this.pos, length);
        this.pos += length;
        return result;
    }

    public string ReadString() {
        ProtoBytes bytes = this.ReadBytes();
        return Encoding.UTF8.GetString(bytes.Buffer, bytes.Offset, bytes.Length);
    }

    public void SkipField(ProtoWireType wireType) {
        switch (wireType) {
        case ProtoWireType.Varint:
            this.ReadVarint64();
            return;
        case ProtoWireType.Fixed64:
            this.pos = checked(this.pos + 8);
            if (this.pos > this.end) throw new EndOfStreamException();
            return;
        case ProtoWireType.LengthDelimited:
            ProtoBytes bytes = this.ReadBytes();
            this.pos = bytes.Offset + bytes.Length;
            return;
        case ProtoWireType.Fixed32:
            this.pos = checked(this.pos + 4);
            if (this.pos > this.end) throw new EndOfStreamException();
            return;
        default:
            throw new NotSupportedException($"Unsupported protobuf wire type {wireType}.");
        }
    }
}

internal sealed class ProtoWriter {
    private readonly Stream stream;

    public ProtoWriter(Stream stream) {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public void WriteTag(uint fieldNumber, ProtoWireType wireType) {
        if (fieldNumber == 0) throw new ArgumentOutOfRangeException(nameof(fieldNumber));
        this.WriteVarint((fieldNumber << 3) | (uint)wireType);
    }

    public void WriteVarint(ulong value) {
        while (value >= 0x80) {
            this.stream.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        this.stream.WriteByte((byte)value);
    }

    public void WriteInt64(long value) => this.WriteVarint(unchecked((ulong)value));

    public void WriteFixed32(uint value) {
        byte[] b = [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];
        this.stream.Write(b, 0, b.Length);
    }

    public void WriteFixed64(ulong value) {
        byte[] b =
        [
            (byte)value,
            (byte)(value >> 8),
            (byte)(value >> 16),
            (byte)(value >> 24),
            (byte)(value >> 32),
            (byte)(value >> 40),
            (byte)(value >> 48),
            (byte)(value >> 56),
        ];
        this.stream.Write(b, 0, b.Length);
    }

    public void WriteDouble(double value) {
        ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        this.WriteFixed64(bits);
    }

    public void WriteFloat(float value) {
        byte[] b = BitConverter.GetBytes(value);
        if (b.Length != 4) throw new InvalidOperationException("Unexpected float size.");
        if (!BitConverter.IsLittleEndian) Array.Reverse(b);
        this.stream.Write(b, 0, b.Length);
    }

    public void WriteString(uint fieldNumber, string value) {
        if (value is null) throw new ArgumentNullException(nameof(value));
        this.WriteTag(fieldNumber, ProtoWireType.LengthDelimited);
        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        this.WriteVarint((ulong)utf8.Length);
        this.stream.Write(utf8, 0, utf8.Length);
    }

    public void WriteBytes(uint fieldNumber, byte[] buffer, int offset, int count) {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if ((uint)offset > (uint)buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if ((uint)count > (uint)(buffer.Length - offset)) throw new ArgumentOutOfRangeException(nameof(count));

        this.WriteTag(fieldNumber, ProtoWireType.LengthDelimited);
        this.WriteVarint((ulong)count);
        if (count != 0) this.stream.Write(buffer, offset, count);
    }

    public void WriteMessage(uint fieldNumber, Action<ProtoWriter> writeMessage) {
        if (writeMessage is null) throw new ArgumentNullException(nameof(writeMessage));

        using var ms = new MemoryStream();
        writeMessage(new ProtoWriter(ms));
        byte[] payload = ms.ToArray();

        this.WriteTag(fieldNumber, ProtoWireType.LengthDelimited);
        this.WriteVarint((ulong)payload.Length);
        if (payload.Length != 0) this.stream.Write(payload, 0, payload.Length);
    }
}